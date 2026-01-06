using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatLake.Core.Services;
using ChatLake.Inference.Similarity;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Logging;
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

    private const string TfidfModelName = "ChatLake.TfIdf.Cosine";
    private const string EmbeddingModelName = "ChatLake.SegmentEmbedding.Cosine";

    // Cache for TF-IDF search functionality
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

        return options.Method switch
        {
            SimilarityMethod.SegmentEmbedding => await CalculateEmbeddingSimilarityAsync(options),
            SimilarityMethod.TfidfCosine => await CalculateTfidfSimilarityAsync(options),
            _ => throw new ArgumentException($"Unknown similarity method: {options.Method}")
        };
    }

    private async Task<SimilarityResult> CalculateEmbeddingSimilarityAsync(SimilarityOptions options)
    {
        var stopwatch = Stopwatch.StartNew();

        // Load conversation embeddings (aggregated from segments)
        ConsoleLog.Info("Similarity", "Loading segment embeddings from database...");
        var conversationEmbeddings = await LoadConversationEmbeddingsAsync();
        ConsoleLog.Info("Similarity", $"Loaded embeddings for {conversationEmbeddings.Count} conversations in {stopwatch.Elapsed.TotalSeconds:F1}s");

        if (conversationEmbeddings.Count < 2)
        {
            ConsoleLog.Warn("Similarity", "Not enough conversations with embeddings to calculate similarity");
            return new SimilarityResult(
                InferenceRunId: 0,
                ConversationsProcessed: conversationEmbeddings.Count,
                PairsCalculated: 0,
                PairsStored: 0,
                Duration: stopwatch.Elapsed);
        }

        var configHash = ComputeConfigHash(options);
        var runId = await _inferenceRuns.StartRunAsync(
            runType: "Similarity",
            modelName: EmbeddingModelName,
            modelVersion: options.ModelVersion,
            inputScope: "All",
            featureConfigHash: configHash,
            inputDescription: $"Calculating embedding similarity for {conversationEmbeddings.Count} conversations");

        try
        {
            // Calculate all pairs above threshold
            ConsoleLog.Info("Similarity", $"Calculating pairs (threshold: {options.MinSimilarityThreshold})...");
            var pairs = CalculateEmbeddingPairs(
                conversationEmbeddings,
                options.MinSimilarityThreshold,
                options.MaxPairsPerConversation);
            ConsoleLog.Info("Similarity", $"Found {pairs.Count} pairs above threshold in {stopwatch.Elapsed.TotalSeconds:F1}s");

            // Store pairs in database
            ConsoleLog.Info("Similarity", $"Storing {pairs.Count} pairs to database...");
            var pairsStored = await StorePairsAsync(runId, pairs, options.Method);
            ConsoleLog.Success("Similarity", $"Stored {pairsStored} pairs in {stopwatch.Elapsed.TotalSeconds:F1}s");

            var metrics = JsonSerializer.Serialize(new
            {
                conversationsProcessed = conversationEmbeddings.Count,
                totalPossiblePairs = conversationEmbeddings.Count * (conversationEmbeddings.Count - 1) / 2,
                pairsAboveThreshold = pairs.Count,
                pairsStored,
                method = "SegmentEmbedding"
            });

            await _inferenceRuns.CompleteRunAsync(runId, metrics);

            // Invalidate cache
            _cacheExpiry = DateTime.MinValue;

            stopwatch.Stop();

            return new SimilarityResult(
                InferenceRunId: runId,
                ConversationsProcessed: conversationEmbeddings.Count,
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

    private async Task<SimilarityResult> CalculateTfidfSimilarityAsync(SimilarityOptions options)
    {
        var stopwatch = Stopwatch.StartNew();

        // Load all conversations with their message text
        ConsoleLog.Info("Similarity", "Loading conversations from database...");
        var conversations = await LoadConversationsAsync();
        ConsoleLog.Info("Similarity", $"Loaded {conversations.Count} conversations in {stopwatch.Elapsed.TotalSeconds:F1}s");

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
            modelName: TfidfModelName,
            modelVersion: options.ModelVersion,
            inputScope: "All",
            featureConfigHash: configHash,
            inputDescription: $"Calculating similarity for {conversations.Count} conversations");

        try
        {
            // Build TF-IDF vectors
            ConsoleLog.Info("Similarity", $"Building TF-IDF vectors for {conversations.Count} conversations...");
            var pipeline = new ConversationSimilarityPipeline(seed: 42);
            pipeline.BuildVectors(conversations);
            ConsoleLog.Info("Similarity", $"Vectors built in {stopwatch.Elapsed.TotalSeconds:F1}s");

            // Calculate all pairs above threshold
            ConsoleLog.Info("Similarity", $"Calculating pairs (threshold: {options.MinSimilarityThreshold})...");
            var pairs = pipeline.CalculateAllPairs(
                options.MinSimilarityThreshold,
                options.MaxPairsPerConversation);
            ConsoleLog.Info("Similarity", $"Found {pairs.Count} pairs in {stopwatch.Elapsed.TotalSeconds:F1}s");

            // Store pairs in database
            ConsoleLog.Info("Similarity", $"Storing {pairs.Count} pairs to database...");
            var pairsStored = await StorePairsAsync(runId, pairs, options.Method);
            ConsoleLog.Success("Similarity", $"Stored in {stopwatch.Elapsed.TotalSeconds:F1}s");

            var metrics = JsonSerializer.Serialize(new
            {
                conversationsProcessed = conversations.Count,
                totalPossiblePairs = conversations.Count * (conversations.Count - 1) / 2,
                pairsAboveThreshold = pairs.Count,
                pairsStored,
                method = "TfidfCosine"
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
        // Load all messages in a single query and group by conversation
        var allMessages = await _db.Messages
            .OrderBy(m => m.ConversationId)
            .ThenBy(m => m.SequenceIndex)
            .Select(m => new { m.ConversationId, m.Content })
            .ToListAsync();

        return allMessages
            .GroupBy(m => m.ConversationId)
            .Select(g => new ConversationTextInput
            {
                ConversationId = g.Key,
                Text = string.Join(" ", g.Select(m => m.Content))
            })
            .ToList();
    }

    private async Task<int> StorePairsAsync(
        long runId,
        IReadOnlyList<SimilarityPair> pairs,
        SimilarityMethod method)
    {
        var methodString = method switch
        {
            SimilarityMethod.TfidfCosine => "TfidfCosine",
            SimilarityMethod.SegmentEmbedding => "SegmentEmbedding",
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
        var modelName = options.Method switch
        {
            SimilarityMethod.SegmentEmbedding => EmbeddingModelName,
            _ => TfidfModelName
        };

        var config = JsonSerializer.Serialize(new
        {
            modelName,
            modelVersion = options.ModelVersion,
            minSimilarityThreshold = options.MinSimilarityThreshold,
            maxPairsPerConversation = options.MaxPairsPerConversation,
            method = options.Method.ToString(),
            seed = 42
        });

        return SHA256.HashData(Encoding.UTF8.GetBytes(config));
    }

    /// <summary>
    /// Load conversation embeddings by aggregating segment embeddings.
    /// Uses average pooling across all segments in a conversation.
    /// </summary>
    private async Task<Dictionary<long, float[]>> LoadConversationEmbeddingsAsync()
    {
        // Load all segment embeddings with their conversation IDs
        var segmentData = await _db.SegmentEmbeddings
            .Join(_db.ConversationSegments,
                se => se.ConversationSegmentId,
                cs => cs.ConversationSegmentId,
                (se, cs) => new { cs.ConversationId, se.EmbeddingVector, se.Dimensions })
            .ToListAsync();

        if (segmentData.Count == 0)
            return new Dictionary<long, float[]>();

        // Group by conversation and average the embeddings
        var dimensions = segmentData.First().Dimensions;
        var conversationEmbeddings = new Dictionary<long, float[]>();

        var grouped = segmentData.GroupBy(s => s.ConversationId);

        foreach (var group in grouped)
        {
            var segments = group.ToList();
            var avgEmbedding = new float[dimensions];

            foreach (var segment in segments)
            {
                var embedding = DeserializeEmbedding(segment.EmbeddingVector, dimensions);
                for (int i = 0; i < dimensions; i++)
                {
                    avgEmbedding[i] += embedding[i];
                }
            }

            // Normalize by segment count (average pooling)
            for (int i = 0; i < dimensions; i++)
            {
                avgEmbedding[i] /= segments.Count;
            }

            // L2 normalize for cosine similarity
            var norm = (float)Math.Sqrt(avgEmbedding.Sum(x => x * x));
            if (norm > 0)
            {
                for (int i = 0; i < dimensions; i++)
                {
                    avgEmbedding[i] /= norm;
                }
            }

            conversationEmbeddings[group.Key] = avgEmbedding;
        }

        return conversationEmbeddings;
    }

    private static float[] DeserializeEmbedding(byte[] bytes, int dimensions)
    {
        var result = new float[dimensions];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    /// <summary>
    /// Calculate similarity pairs using pre-computed embeddings.
    /// Uses SIMD-accelerated cosine similarity when available.
    /// </summary>
    private static List<SimilarityPair> CalculateEmbeddingPairs(
        Dictionary<long, float[]> embeddings,
        decimal threshold,
        int maxPairsPerConversation)
    {
        var conversationIds = embeddings.Keys.OrderBy(k => k).ToList();
        var n = conversationIds.Count;
        var totalPairs = (long)n * (n - 1) / 2;

        ConsoleLog.Info("Similarity", $"Calculating {totalPairs:N0} pairs using SIMD-accelerated cosine similarity...");

        // Track top pairs per conversation
        var topPairs = new Dictionary<long, List<(long otherId, decimal similarity)>>();
        foreach (var id in conversationIds)
        {
            topPairs[id] = new List<(long, decimal)>();
        }

        // Calculate similarities in parallel
        var lockObj = new object();
        var pairsFound = 0;

        Parallel.For(0, n, i =>
        {
            var idA = conversationIds[i];
            var embA = embeddings[idA];
            var localPairs = new List<(long idB, decimal sim)>();

            for (int j = i + 1; j < n; j++)
            {
                var idB = conversationIds[j];
                var embB = embeddings[idB];

                // Cosine similarity (embeddings are already normalized)
                var similarity = CosineSimilarity(embA, embB);

                if (similarity >= (float)threshold)
                {
                    localPairs.Add((idB, (decimal)similarity));
                }
            }

            if (localPairs.Count > 0)
            {
                lock (lockObj)
                {
                    foreach (var (idB, sim) in localPairs)
                    {
                        topPairs[idA].Add((idB, sim));
                        topPairs[idB].Add((idA, sim));
                        pairsFound++;
                    }
                }
            }
        });

        // Build final pairs list respecting maxPairsPerConversation
        var result = new HashSet<(long, long)>();
        var finalPairs = new List<SimilarityPair>();

        foreach (var (convId, pairs) in topPairs)
        {
            var topN = pairs
                .OrderByDescending(p => p.similarity)
                .Take(maxPairsPerConversation);

            foreach (var (otherId, sim) in topN)
            {
                // Normalize order
                var (a, b) = convId < otherId ? (convId, otherId) : (otherId, convId);

                if (result.Add((a, b)))
                {
                    finalPairs.Add(new SimilarityPair(a, b, sim));
                }
            }
        }

        return finalPairs;
    }

    /// <summary>
    /// Calculate cosine similarity using SIMD when available.
    /// Assumes vectors are already L2-normalized.
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<float>.Count)
        {
            var sum = Vector<float>.Zero;
            var i = 0;

            // SIMD loop
            for (; i <= a.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                var va = new Vector<float>(a, i);
                var vb = new Vector<float>(b, i);
                sum += va * vb;
            }

            var dotProduct = Vector.Dot(sum, Vector<float>.One);

            // Handle remainder
            for (; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
            }

            return dotProduct;
        }
        else
        {
            // Scalar fallback
            float dotProduct = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
            }
            return dotProduct;
        }
    }
}
