using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatLake.Core.Services;
using ChatLake.Inference.Similarity;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

/// <summary>
/// Service for calculating and querying conversation similarity.
/// </summary>
public sealed class SimilarityService : ISimilarityService
{
    private readonly ChatLakeDbContext _db;
    private readonly IInferenceRunService _inferenceRuns;

    private const string ModelName = "ChatLake.TfIdf.Cosine";

    // Cache for search functionality
    private ConversationSimilarityPipeline? _cachedPipeline;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public SimilarityService(ChatLakeDbContext db, IInferenceRunService inferenceRuns)
    {
        _db = db;
        _inferenceRuns = inferenceRuns;
    }

    public async Task<SimilarityResult> CalculateSimilarityAsync(SimilarityOptions? options = null)
    {
        options ??= new SimilarityOptions();
        var stopwatch = Stopwatch.StartNew();

        // Load all conversations with their message text
        var conversations = await LoadConversationsAsync();

        if (conversations.Count < 2)
        {
            return new SimilarityResult(
                InferenceRunId: 0,
                ConversationsProcessed: conversations.Count,
                PairsCalculated: 0,
                PairsStored: 0,
                Duration: stopwatch.Elapsed);
        }

        var configHash = ComputeConfigHash(options);
        var runId = await _inferenceRuns.StartRunAsync(
            runType: "Similarity",
            modelName: ModelName,
            modelVersion: options.ModelVersion,
            inputScope: "All",
            featureConfigHash: configHash,
            inputDescription: $"Calculating similarity for {conversations.Count} conversations");

        try
        {
            // Build TF-IDF vectors
            var pipeline = new ConversationSimilarityPipeline(seed: 42);
            pipeline.BuildVectors(conversations);

            // Calculate all pairs above threshold
            var pairs = pipeline.CalculateAllPairs(
                options.MinSimilarityThreshold,
                options.MaxPairsPerConversation);

            // Store pairs in database
            var pairsStored = await StorePairsAsync(runId, pairs, options.Method);

            var metrics = JsonSerializer.Serialize(new
            {
                conversationsProcessed = conversations.Count,
                totalPossiblePairs = conversations.Count * (conversations.Count - 1) / 2,
                pairsAboveThreshold = pairs.Count,
                pairsStored
            });

            await _inferenceRuns.CompleteRunAsync(runId, metrics);

            // Invalidate cache
            _cacheExpiry = DateTime.MinValue;

            stopwatch.Stop();

            return new SimilarityResult(
                InferenceRunId: runId,
                ConversationsProcessed: conversations.Count,
                PairsCalculated: pairs.Count,
                PairsStored: pairsStored,
                Duration: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            await _inferenceRuns.FailRunAsync(runId, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<SimilarConversationDto>> FindSimilarAsync(long conversationId, int limit = 10)
    {
        // First try to get from stored similarities
        var storedSimilarities = await _db.ConversationSimilarities
            .Where(cs => cs.ConversationIdA == conversationId || cs.ConversationIdB == conversationId)
            .OrderByDescending(cs => cs.Similarity)
            .Take(limit)
            .ToListAsync();

        if (storedSimilarities.Count > 0)
        {
            var similarIds = storedSimilarities
                .Select(cs => cs.ConversationIdA == conversationId ? cs.ConversationIdB : cs.ConversationIdA)
                .ToList();

            return await GetConversationDetailsAsync(similarIds, storedSimilarities
                .ToDictionary(
                    cs => cs.ConversationIdA == conversationId ? cs.ConversationIdB : cs.ConversationIdA,
                    cs => cs.Similarity));
        }

        // Fall back to real-time calculation using cached pipeline
        var pipeline = await GetOrBuildPipelineAsync();
        if (pipeline == null)
            return [];

        // Get the conversation text
        var convText = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SequenceIndex)
            .Select(m => m.Content)
            .ToListAsync();

        if (convText.Count == 0)
            return [];

        var queryText = string.Join(" ", convText);
        var matches = pipeline.FindSimilar(queryText, limit + 1); // +1 to exclude self

        var results = matches
            .Where(m => m.ConversationId != conversationId)
            .Take(limit)
            .ToList();

        return await GetConversationDetailsAsync(
            results.Select(r => r.ConversationId).ToList(),
            results.ToDictionary(r => r.ConversationId, r => r.Similarity));
    }

    public async Task<IReadOnlyList<SimilarConversationDto>> SearchSimilarAsync(string queryText, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return [];

        var pipeline = await GetOrBuildPipelineAsync();
        if (pipeline == null)
            return [];

        var matches = pipeline.FindSimilar(queryText, limit);

        return await GetConversationDetailsAsync(
            matches.Select(m => m.ConversationId).ToList(),
            matches.ToDictionary(m => m.ConversationId, m => m.Similarity));
    }

    public async Task<decimal?> GetSimilarityAsync(long conversationIdA, long conversationIdB)
    {
        // Normalize order
        if (conversationIdA > conversationIdB)
            (conversationIdA, conversationIdB) = (conversationIdB, conversationIdA);

        // Check stored similarities first
        var stored = await _db.ConversationSimilarities
            .Where(cs => cs.ConversationIdA == conversationIdA && cs.ConversationIdB == conversationIdB)
            .FirstOrDefaultAsync();

        if (stored != null)
            return stored.Similarity;

        // Fall back to real-time calculation
        var pipeline = await GetOrBuildPipelineAsync();
        return pipeline?.GetSimilarity(conversationIdA, conversationIdB);
    }

    private async Task<List<ConversationTextInput>> LoadConversationsAsync()
    {
        var conversations = await _db.Conversations
            .Select(c => new { c.ConversationId })
            .ToListAsync();

        var result = new List<ConversationTextInput>();

        foreach (var conv in conversations)
        {
            var messages = await _db.Messages
                .Where(m => m.ConversationId == conv.ConversationId)
                .OrderBy(m => m.SequenceIndex)
                .Select(m => m.Content)
                .ToListAsync();

            if (messages.Count == 0)
                continue;

            result.Add(new ConversationTextInput
            {
                ConversationId = conv.ConversationId,
                Text = string.Join(" ", messages)
            });
        }

        return result;
    }

    private async Task<int> StorePairsAsync(
        long runId,
        IReadOnlyList<SimilarityPair> pairs,
        SimilarityMethod method)
    {
        var methodString = method switch
        {
            SimilarityMethod.TfidfCosine => "TfidfCosine",
            _ => "Unknown"
        };

        foreach (var pair in pairs)
        {
            _db.ConversationSimilarities.Add(new ConversationSimilarity
            {
                InferenceRunId = runId,
                ConversationIdA = pair.ConversationIdA,
                ConversationIdB = pair.ConversationIdB,
                Similarity = pair.Similarity,
                Method = methodString
            });
        }

        await _db.SaveChangesAsync();
        return pairs.Count;
    }

    private async Task<IReadOnlyList<SimilarConversationDto>> GetConversationDetailsAsync(
        List<long> conversationIds,
        Dictionary<long, decimal> similarities)
    {
        var summaries = await _db.ConversationSummaries
            .Where(cs => conversationIds.Contains(cs.ConversationId))
            .ToDictionaryAsync(cs => cs.ConversationId);

        return conversationIds
            .Where(id => summaries.ContainsKey(id))
            .Select(id =>
            {
                var summary = summaries[id];
                return new SimilarConversationDto(
                    ConversationId: id,
                    Title: summary.PreviewText?.Split('\n').FirstOrDefault(),
                    PreviewText: summary.PreviewText,
                    Similarity: similarities.GetValueOrDefault(id, 0m),
                    FirstMessageAtUtc: summary.FirstMessageAtUtc,
                    MessageCount: summary.MessageCount);
            })
            .OrderByDescending(s => s.Similarity)
            .ToList();
    }

    private async Task<ConversationSimilarityPipeline?> GetOrBuildPipelineAsync()
    {
        if (_cachedPipeline != null && DateTime.UtcNow < _cacheExpiry)
            return _cachedPipeline;

        var conversations = await LoadConversationsAsync();
        if (conversations.Count == 0)
            return null;

        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        pipeline.BuildVectors(conversations);

        _cachedPipeline = pipeline;
        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

        return pipeline;
    }

    private static byte[] ComputeConfigHash(SimilarityOptions options)
    {
        var config = JsonSerializer.Serialize(new
        {
            modelName = ModelName,
            modelVersion = options.ModelVersion,
            minSimilarityThreshold = options.MinSimilarityThreshold,
            maxPairsPerConversation = options.MaxPairsPerConversation,
            method = options.Method.ToString(),
            seed = 42
        });

        return SHA256.HashData(Encoding.UTF8.GetBytes(config));
    }
}
