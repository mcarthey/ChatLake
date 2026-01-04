using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatLake.Core.Services;
using ChatLake.Inference.Clustering;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

public sealed class ClusteringService : IClusteringService
{
    private readonly ChatLakeDbContext _db;
    private readonly IInferenceRunService _inferenceRunService;

    private const string ModelName = "ChatLake.TfIdf.KMeans";

    public ClusteringService(ChatLakeDbContext db, IInferenceRunService inferenceRunService)
    {
        _db = db;
        _inferenceRunService = inferenceRunService;
    }

    public async Task<ClusteringResult> ClusterConversationsAsync(ClusteringOptions? options = null)
    {
        options ??= new ClusteringOptions();
        var stopwatch = Stopwatch.StartNew();

        // Load conversations with their message text
        var conversationTexts = await LoadConversationTextsAsync();

        if (conversationTexts.Count == 0)
        {
            return new ClusteringResult(
                InferenceRunId: 0,
                ConversationCount: 0,
                ClusterCount: 0,
                SuggestionsCreated: 0,
                Duration: stopwatch.Elapsed);
        }

        // Determine cluster count
        var clusterCount = options.ClusterCount ?? CalculateOptimalClusterCount(conversationTexts.Count);

        // Compute feature config hash for reproducibility
        var configHash = ComputeConfigHash(options, clusterCount);

        // Start inference run
        var runId = await _inferenceRunService.StartRunAsync(
            runType: "Clustering",
            modelName: ModelName,
            modelVersion: options.ModelVersion,
            inputScope: "All",
            featureConfigHash: configHash,
            inputDescription: $"Clustering {conversationTexts.Count} conversations into {clusterCount} groups");

        try
        {
            // Run the ML pipeline
            var pipeline = new ConversationClusteringPipeline(seed: 42); // Fixed seed for reproducibility
            var result = pipeline.Cluster(conversationTexts, clusterCount, options.MaxIterations);

            // Create ProjectSuggestion records
            var suggestionsCreated = await CreateProjectSuggestionsAsync(
                runId, result.ClusterStats, conversationTexts, options.AutoAcceptThreshold);

            // Complete the run with metrics
            var metrics = new
            {
                conversationCount = conversationTexts.Count,
                clusterCount,
                suggestionsCreated,
                nonEmptyClusters = result.ClusterStats.Count(c => c.Count > 0),
                avgClusterSize = result.ClusterStats.Where(c => c.Count > 0).Average(c => c.Count),
                avgConfidence = result.ClusterStats.Where(c => c.Count > 0).Average(c => (double)c.Confidence)
            };

            await _inferenceRunService.CompleteRunAsync(runId, JsonSerializer.Serialize(metrics));

            stopwatch.Stop();
            return new ClusteringResult(
                InferenceRunId: runId,
                ConversationCount: conversationTexts.Count,
                ClusterCount: clusterCount,
                SuggestionsCreated: suggestionsCreated,
                Duration: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            await _inferenceRunService.FailRunAsync(runId, ex.Message);
            throw;
        }
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
            // Get sample conversation IDs for this suggestion
            // For now, return empty - would need a mapping table to track which conversations belong to which suggestion
            result.Add(new ClusterSummary(
                ProjectSuggestionId: suggestion.ProjectSuggestionId,
                SuggestedName: suggestion.SuggestedName,
                SuggestedProjectKey: suggestion.SuggestedProjectKey,
                ConversationCount: 0, // Would need to store this
                Confidence: suggestion.Confidence,
                Status: suggestion.Status,
                SampleConversationIds: Array.Empty<long>()));
        }

        return result;
    }

    private async Task<List<ConversationTextInput>> LoadConversationTextsAsync()
    {
        // Load conversations that aren't already assigned to a project
        var assignedConversationIds = await _db.ProjectConversations
            .Where(pc => pc.IsCurrent)
            .Select(pc => pc.ConversationId)
            .ToListAsync();

        var conversations = await _db.Conversations
            .Where(c => !assignedConversationIds.Contains(c.ConversationId))
            .Select(c => new { c.ConversationId })
            .ToListAsync();

        var result = new List<ConversationTextInput>();

        foreach (var conv in conversations)
        {
            var messages = await _db.Messages
                .Where(m => m.ConversationId == conv.ConversationId)
                .OrderBy(m => m.SequenceIndex)
                .Select(m => new { m.Role, m.Content })
                .ToListAsync();

            if (messages.Count == 0)
                continue;

            // Concatenate all message content
            var text = string.Join("\n\n", messages.Select(m => m.Content));

            // Get title from first user message
            var title = messages
                .FirstOrDefault(m => m.Role == "user")?.Content
                .Split('\n').FirstOrDefault()?.Trim();

            if (title?.Length > 100)
                title = title[..100];

            result.Add(new ConversationTextInput
            {
                ConversationId = conv.ConversationId,
                Text = text,
                Title = title
            });
        }

        return result;
    }

    private async Task<int> CreateProjectSuggestionsAsync(
        long runId,
        IReadOnlyList<ClusterStats> clusterStats,
        List<ConversationTextInput> conversationTexts,
        decimal autoAcceptThreshold)
    {
        var suggestionsCreated = 0;

        foreach (var cluster in clusterStats.Where(c => c.Count > 0))
        {
            // Generate cluster name from sample conversations
            var sampleIds = cluster.ConversationIds.Take(5).ToList();
            var sampleTitles = conversationTexts
                .Where(c => sampleIds.Contains(c.ConversationId))
                .Select(c => c.Title)
                .Where(t => !string.IsNullOrEmpty(t))
                .Take(3)
                .ToList();

            var suggestedName = GenerateClusterName(sampleTitles, cluster.ClusterId);
            var suggestedKey = GenerateProjectKey(suggestedName);

            var suggestion = new ProjectSuggestion
            {
                InferenceRunId = runId,
                SuggestedProjectKey = suggestedKey,
                SuggestedName = suggestedName,
                Summary = $"Cluster of {cluster.Count} related conversations",
                Confidence = cluster.Confidence,
                Status = cluster.Confidence >= autoAcceptThreshold ? "Accepted" : "Pending"
            };

            _db.ProjectSuggestions.Add(suggestion);
            suggestionsCreated++;
        }

        await _db.SaveChangesAsync();
        return suggestionsCreated;
    }

    private static string GenerateClusterName(List<string?> sampleTitles, uint clusterId)
    {
        if (sampleTitles.Count > 0 && !string.IsNullOrEmpty(sampleTitles[0]))
        {
            // Use first title as base, truncate if needed
            var baseName = sampleTitles[0]!;
            if (baseName.Length > 50)
                baseName = baseName[..50] + "...";
            return baseName;
        }

        return $"Cluster {clusterId}";
    }

    private static string GenerateProjectKey(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace("...", "");

        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        // Truncate and add timestamp for uniqueness
        if (slug.Length > 50)
            slug = slug[..50];

        return $"{slug}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private static int CalculateOptimalClusterCount(int conversationCount)
    {
        // Heuristic: sqrt(n/2) gives reasonable cluster count
        var k = (int)Math.Ceiling(Math.Sqrt(conversationCount / 2.0));
        return Math.Max(2, Math.Min(k, 100)); // Clamp between 2 and 100
    }

    private static byte[] ComputeConfigHash(ClusteringOptions options, int clusterCount)
    {
        var config = JsonSerializer.Serialize(new
        {
            modelName = ModelName,
            modelVersion = options.ModelVersion,
            clusterCount,
            maxIterations = options.MaxIterations,
            seed = 42
        });

        return SHA256.HashData(Encoding.UTF8.GetBytes(config));
    }
}
