using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatLake.Core.Services;
using ChatLake.Inference.Clustering;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Logging;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

public sealed class ClusteringService : IClusteringService
{
    private readonly ChatLakeDbContext _db;
    private readonly IInferenceRunService _inferenceRunService;
    private readonly ISegmentationService _segmentationService;
    private readonly IEmbeddingCacheService _embeddingCacheService;
    private readonly ILlmService? _llmService;

    private const string SegmentModelName = "ChatLake.Segments.UMAP-HDBSCAN";

    public ClusteringService(
        ChatLakeDbContext db,
        IInferenceRunService inferenceRunService,
        ISegmentationService segmentationService,
        IEmbeddingCacheService embeddingCacheService,
        ILlmService? llmService = null)
    {
        _db = db;
        _inferenceRunService = inferenceRunService;
        _segmentationService = segmentationService;
        _embeddingCacheService = embeddingCacheService;
        _llmService = llmService;
    }

    public async Task<ClusteringResult> ClusterConversationsAsync(ClusteringOptions? options = null)
    {
        options ??= new ClusteringOptions();
        var stopwatch = Stopwatch.StartNew();

        // Phase 1: Segmentation
        ConsoleLog.Info("Clustering", "Phase 1: Segmenting conversations...");
        var segmentationResult = await _segmentationService.SegmentConversationsAsync();
        ConsoleLog.Success("Clustering", $"Created {segmentationResult.SegmentsCreated} segments from {segmentationResult.ConversationsProcessed} new conversations");

        // Phase 2: Generate embeddings for segments
        ConsoleLog.Info("Clustering", "Phase 2: Generating segment embeddings...");
        var embeddingResult = await _embeddingCacheService.GenerateMissingEmbeddingsAsync();
        ConsoleLog.Success("Clustering", $"Generated {embeddingResult.EmbeddingsGenerated} new embeddings, {embeddingResult.EmbeddingsCached} cached");

        // Phase 3: Load all segment embeddings for clustering
        ConsoleLog.Info("Clustering", "Phase 3: Loading embeddings for clustering...");
        var segmentEmbeddings = await _embeddingCacheService.GetAllEmbeddingsAsync();

        if (segmentEmbeddings.Count == 0)
        {
            ConsoleLog.Warn("Clustering", "No segments with embeddings to cluster");
            return new ClusteringResult(
                InferenceRunId: 0,
                SegmentCount: 0,
                ConversationCount: 0,
                ClusterCount: 0,
                SuggestionsCreated: 0,
                NoiseCount: 0,
                Duration: stopwatch.Elapsed);
        }

        ConsoleLog.Info("Clustering", $"Loaded {segmentEmbeddings.Count} segment embeddings");

        // Compute feature config hash
        var configHash = ComputeConfigHash(options);

        // Start inference run for clustering
        var runId = await _inferenceRunService.StartRunAsync(
            runType: "SegmentClustering",
            modelName: SegmentModelName,
            modelVersion: options.ModelVersion,
            inputScope: "AllSegments",
            featureConfigHash: configHash,
            inputDescription: $"UMAP→HDBSCAN clustering {segmentEmbeddings.Count} segments (umap={options.UmapDimensions}D, minClusterSize={options.MinClusterSize})");

        try
        {
            // Phase 4: Run UMAP + HDBSCAN clustering on segment embeddings
            ConsoleLog.Info("Clustering", "Phase 4: Running UMAP dimensionality reduction + HDBSCAN clustering...");
            var embeddingInputs = segmentEmbeddings
                .Select(e => new EmbeddingInput
                {
                    ConversationId = e.ConversationSegmentId, // Repurpose field for segment ID
                    Features = e.Embedding
                })
                .ToList();

            var pipelineOptions = new UmapHdbscanOptions
            {
                UmapDimensions = options.UmapDimensions,
                UmapNeighbors = options.UmapNeighbors,
                MinClusterSize = options.MinClusterSize,
                MinPoints = options.MinPoints,
                RandomSeed = options.RandomSeed
            };

            var pipeline = new UmapHdbscanPipeline();
            var result = pipeline.Cluster(embeddingInputs, pipelineOptions);

            ConsoleLog.Success("Clustering", $"UMAP+HDBSCAN found {result.ClusterCount} natural clusters");
            ConsoleLog.Info("Clustering", $"{result.NoiseSegmentIds.Count} segments identified as noise (don't fit any cluster)");

            // Phase 5: Create ProjectSuggestions
            ConsoleLog.Info("Clustering", "Phase 5: Creating project suggestions...");
            var suggestionsCreated = await CreateUmapHdbscanProjectSuggestionsAsync(
                runId, result.ClusterStats, segmentEmbeddings, options.AutoAcceptThreshold);

            // Complete the run with metrics
            var uniqueConversations = segmentEmbeddings.Select(e => e.ConversationId).Distinct().Count();
            var metrics = new
            {
                algorithm = "UMAP+HDBSCAN",
                umapDimensions = options.UmapDimensions,
                umapNeighbors = options.UmapNeighbors,
                segmentCount = segmentEmbeddings.Count,
                uniqueConversationCount = uniqueConversations,
                clusterCount = result.ClusterCount,
                noiseCount = result.NoiseSegmentIds.Count,
                noisePercentage = Math.Round(result.NoiseSegmentIds.Count * 100.0 / segmentEmbeddings.Count, 1),
                suggestionsCreated,
                avgClusterSize = result.ClusterStats.Count > 0
                    ? result.ClusterStats.Average(c => c.Count)
                    : 0,
                avgConfidence = result.ClusterStats.Count > 0
                    ? result.ClusterStats.Average(c => (double)c.Confidence)
                    : 0
            };

            await _inferenceRunService.CompleteRunAsync(runId, JsonSerializer.Serialize(metrics));

            stopwatch.Stop();
            ConsoleLog.Success("Clustering", $"Complete: {suggestionsCreated} suggestions from {result.ClusterCount} clusters ({result.NoiseSegmentIds.Count} noise) in {stopwatch.Elapsed.TotalSeconds:F1}s");

            return new ClusteringResult(
                InferenceRunId: runId,
                SegmentCount: segmentEmbeddings.Count,
                ConversationCount: uniqueConversations,
                ClusterCount: result.ClusterCount,
                SuggestionsCreated: suggestionsCreated,
                NoiseCount: result.NoiseSegmentIds.Count,
                Duration: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            await _inferenceRunService.FailRunAsync(runId, ex.Message);
            throw;
        }
    }

    private async Task<int> CreateUmapHdbscanProjectSuggestionsAsync(
        long runId,
        IReadOnlyList<UmapHdbscanClusterStats> clusterStats,
        IReadOnlyList<SegmentEmbeddingData> segmentEmbeddings,
        decimal autoAcceptThreshold)
    {
        var suggestionsCreated = 0;

        // Build lookup from segment ID to conversation ID
        var segmentToConversation = segmentEmbeddings.ToDictionary(
            e => e.ConversationSegmentId,
            e => e.ConversationId);

        // Check if LLM is available for naming
        var useLlm = _llmService != null && await _llmService.IsAvailableAsync();
        if (useLlm)
            ConsoleLog.Info("Clustering", "Using LLM for cluster naming");
        else
            ConsoleLog.Warn("Clustering", "LLM not available, using fallback naming");

        foreach (var cluster in clusterStats.OrderByDescending(c => c.Count))
        {
            var segmentIds = cluster.SegmentIds;

            // Get unique conversation IDs for these segments
            var conversationIds = segmentIds
                .Where(sid => segmentToConversation.ContainsKey(sid))
                .Select(sid => segmentToConversation[sid])
                .Distinct()
                .ToList();

            // Load segment content for naming
            var segmentContents = await _db.ConversationSegments
                .Where(s => segmentIds.Contains(s.ConversationSegmentId))
                .Select(s => new { s.ConversationSegmentId, s.ContentText })
                .ToListAsync();

            // Generate name
            string suggestedName;
            if (useLlm && segmentContents.Count > 0)
            {
                // Sample more segments for better theme detection
                var samples = segmentContents
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(12)
                    .Select(s => s.ContentText.Length > 1000 ? s.ContentText[..1000] : s.ContentText)
                    .ToList();

                suggestedName = await _llmService!.GenerateClusterNameAsync(samples, segmentIds.Count);
                ConsoleLog.Info("Clustering", $"Cluster {cluster.ClusterId}: \"{suggestedName}\" ({segmentIds.Count} segments, {conversationIds.Count} convos, {cluster.Confidence:P0})");
            }
            else
            {
                suggestedName = $"Topic {cluster.ClusterId}";
            }

            var suggestedKey = GenerateProjectKey(suggestedName);

            // Generate summary
            var samplePreviews = segmentContents
                .Take(3)
                .Select(s => CleanPreview(s.ContentText))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            var summary = samplePreviews.Count > 0
                ? $"{segmentIds.Count} segments from {conversationIds.Count} conversations. Samples: {string.Join("; ", samplePreviews)}"
                : $"{segmentIds.Count} segments from {conversationIds.Count} conversations";

            var suggestion = new ProjectSuggestion
            {
                InferenceRunId = runId,
                SuggestedProjectKey = suggestedKey,
                SuggestedName = suggestedName,
                Summary = summary,
                Confidence = cluster.Confidence,
                Status = cluster.Confidence >= autoAcceptThreshold ? "Accepted" : "Pending",
                ConversationIdsJson = JsonSerializer.Serialize(conversationIds),
                SegmentIdsJson = JsonSerializer.Serialize(segmentIds),
                UniqueConversationCount = conversationIds.Count
            };

            _db.ProjectSuggestions.Add(suggestion);
            suggestionsCreated++;
        }

        await _db.SaveChangesAsync();
        return suggestionsCreated;
    }

    private static string CleanPreview(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";

        // Skip JSON-like content
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("[")) return "";

        // Get first line or first 80 chars
        var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";
        if (firstLine.Length > 80)
            firstLine = firstLine[..80] + "...";

        return firstLine;
    }

    public async Task<IReadOnlyList<ClusterSummary>> GetClusterSummariesAsync(long inferenceRunId)
    {
        var suggestions = await _db.ProjectSuggestions
            .Where(ps => ps.InferenceRunId == inferenceRunId)
            .OrderByDescending(ps => ps.Confidence)
            .ToListAsync();

        var result = new List<ClusterSummary>();

        foreach (var suggestion in suggestions)
        {
            var segmentIds = !string.IsNullOrEmpty(suggestion.SegmentIdsJson)
                ? JsonSerializer.Deserialize<List<long>>(suggestion.SegmentIdsJson) ?? []
                : [];

            result.Add(new ClusterSummary(
                ProjectSuggestionId: suggestion.ProjectSuggestionId,
                SuggestedName: suggestion.SuggestedName,
                SuggestedProjectKey: suggestion.SuggestedProjectKey,
                ConversationCount: suggestion.UniqueConversationCount,
                Confidence: suggestion.Confidence,
                Status: suggestion.Status,
                SampleConversationIds: segmentIds.Take(5).ToArray()));
        }

        return result;
    }

    private static string GenerateProjectKey(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace("...", "");

        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        if (slug.Length > 50)
            slug = slug[..50];

        return $"{slug}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private static byte[] ComputeConfigHash(ClusteringOptions options)
    {
        var config = JsonSerializer.Serialize(new
        {
            algorithm = "UMAP+HDBSCAN",
            modelVersion = options.ModelVersion,
            umapDimensions = options.UmapDimensions,
            umapNeighbors = options.UmapNeighbors,
            minClusterSize = options.MinClusterSize,
            minPoints = options.MinPoints,
            randomSeed = options.RandomSeed
        });

        return SHA256.HashData(Encoding.UTF8.GetBytes(config));
    }
}
