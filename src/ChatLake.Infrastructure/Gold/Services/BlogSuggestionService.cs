using System.Diagnostics;
using System.Text.Json;
using ChatLake.Core.Models;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Logging;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

public sealed class BlogSuggestionService : IBlogSuggestionService
{
    private readonly ChatLakeDbContext _db;
    private readonly ILlmService _llm;

    public BlogSuggestionService(ChatLakeDbContext db, ILlmService llm)
    {
        _db = db;
        _llm = llm;
    }

    public async Task<BlogGenerationResult> EvaluateAndGenerateAsync(
        BlogGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new BlogGenerationOptions();
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        var generatedIds = new List<long>();

        // Create inference run for tracking
        var configHash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"blog-{options.MinOverallScore}-{options.TargetWordCount}"));

        var inferenceRun = new InferenceRun
        {
            RunType = "BlogGeneration",
            ModelName = "ollama",
            ModelVersion = "mistral:7b",
            InputScope = "AcceptedClusters",
            FeatureConfigHashSha256 = configHash,
            Status = "Running",
            StartedAtUtc = DateTime.UtcNow
        };
        _db.InferenceRuns.Add(inferenceRun);
        await _db.SaveChangesAsync(cancellationToken);

        ConsoleLog.Info("Blog", "Starting blog evaluation and generation...");

        try
        {
            // Get accepted ProjectSuggestions with enough segments
            var eligibleClusters = await _db.ProjectSuggestions
                .Where(ps => ps.Status == "Accepted")
                .Where(ps => !string.IsNullOrEmpty(ps.SegmentIdsJson))
                .OrderByDescending(ps => ps.Confidence)
                .Take(options.MaxClustersToEvaluate)
                .ToListAsync(cancellationToken);

            ConsoleLog.Info("Blog", $"Found {eligibleClusters.Count} eligible clusters to evaluate");

            var clustersEvaluated = 0;
            var clustersPassed = 0;

            foreach (var cluster in eligibleClusters)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var segmentIds = JsonSerializer.Deserialize<List<long>>(cluster.SegmentIdsJson!) ?? [];

                    if (segmentIds.Count < options.MinSegmentCount)
                    {
                        ConsoleLog.Info("Blog", $"Skipping '{cluster.SuggestedName}' - only {segmentIds.Count} segments");
                        continue;
                    }

                    // Load segment content
                    var segments = await _db.ConversationSegments
                        .Where(s => segmentIds.Contains(s.ConversationSegmentId))
                        .Select(s => s.ContentText)
                        .ToListAsync(cancellationToken);

                    if (segments.Count < options.MinSegmentCount)
                    {
                        ConsoleLog.Info("Blog", $"Skipping '{cluster.SuggestedName}' - only {segments.Count} segments found");
                        continue;
                    }

                    clustersEvaluated++;
                    ConsoleLog.Info("Blog", $"Evaluating cluster: {cluster.SuggestedName} ({segments.Count} segments)");

                    // Evaluate for blog-worthiness
                    var scores = await _llm.EvaluateClusterForBlogAsync(
                        segments, cluster.SuggestedName, cancellationToken);

                    ConsoleLog.Info("Blog", $"  Score: {scores.OverallScore:F2} " +
                        $"(Ed:{scores.EducationalValue:F2} PS:{scores.ProblemSolvingDepth:F2} " +
                        $"TC:{scores.TopicCoherence:F2} CC:{scores.ContentCompleteness:F2} RI:{scores.ReaderInterest:F2})");

                    if (scores.OverallScore < options.MinOverallScore)
                    {
                        ConsoleLog.Info("Blog", $"  → Below threshold ({options.MinOverallScore:F2}), skipping");
                        continue;
                    }

                    clustersPassed++;
                    ConsoleLog.Info("Blog", $"  → Generating blog content...");

                    // Generate blog content
                    var title = await _llm.GenerateBlogTitleAsync(
                        segments, cluster.SuggestedName, cancellationToken);
                    ConsoleLog.Info("Blog", $"  Title: {title}");

                    var outline = await _llm.GenerateBlogOutlineAsync(
                        segments, title, cancellationToken);

                    var content = await _llm.GenerateBlogContentAsync(
                        segments, title, outline, options.TargetWordCount, cancellationToken);

                    var wordCount = content.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
                    ConsoleLog.Info("Blog", $"  Generated {wordCount} words");

                    // Get conversation IDs for the blog
                    var conversationIds = await _db.ConversationSegments
                        .Where(s => segmentIds.Contains(s.ConversationSegmentId))
                        .Select(s => s.ConversationId)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    // Save blog suggestion
                    var blogSuggestion = new BlogTopicSuggestion
                    {
                        InferenceRunId = inferenceRun.InferenceRunId,
                        ProjectSuggestionId = cluster.ProjectSuggestionId,
                        Title = title,
                        OutlineJson = JsonSerializer.Serialize(outline),
                        BlogContentMarkdown = content,
                        EvaluationScoreJson = JsonSerializer.Serialize(scores),
                        Confidence = scores.OverallScore,
                        SourceConversationIdsJson = JsonSerializer.Serialize(conversationIds),
                        SourceSegmentIdsJson = cluster.SegmentIdsJson,
                        WordCount = wordCount,
                        Status = "Pending",
                        GeneratedAtUtc = DateTime.UtcNow
                    };

                    _db.BlogTopicSuggestions.Add(blogSuggestion);
                    await _db.SaveChangesAsync(cancellationToken);

                    generatedIds.Add(blogSuggestion.BlogTopicSuggestionId);
                    ConsoleLog.Success("Blog", $"  → Saved blog suggestion #{blogSuggestion.BlogTopicSuggestionId}");
                }
                catch (Exception ex)
                {
                    var error = $"Error processing cluster '{cluster.SuggestedName}': {ex.Message}";
                    ConsoleLog.Error("Blog", error);
                    errors.Add(error);
                }
            }

            sw.Stop();

            // Update inference run
            inferenceRun.Status = "Completed";
            inferenceRun.CompletedAtUtc = DateTime.UtcNow;
            inferenceRun.MetricsJson = JsonSerializer.Serialize(new
            {
                clustersEvaluated,
                clustersPassed,
                blogsGenerated = generatedIds.Count,
                elapsedMs = sw.ElapsedMilliseconds,
                errors = errors.Count
            });
            await _db.SaveChangesAsync(cancellationToken);

            ConsoleLog.Success("Blog", $"Blog generation complete: {generatedIds.Count} blogs from {clustersEvaluated} clusters in {sw.Elapsed.TotalSeconds:F1}s");

            return new BlogGenerationResult
            {
                ClustersEvaluated = clustersEvaluated,
                ClustersPassed = clustersPassed,
                BlogsGenerated = generatedIds.Count,
                ElapsedTime = sw.Elapsed,
                GeneratedBlogIds = generatedIds,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            inferenceRun.Status = "Failed";
            inferenceRun.CompletedAtUtc = DateTime.UtcNow;
            inferenceRun.MetricsJson = JsonSerializer.Serialize(new { error = ex.Message });
            await _db.SaveChangesAsync(CancellationToken.None);

            throw;
        }
    }

    public async Task<BlogTopicSuggestionDto> RegenerateContentAsync(
        long blogTopicSuggestionId,
        CancellationToken cancellationToken = default)
    {
        var blog = await _db.BlogTopicSuggestions
            .Include(b => b.ProjectSuggestion)
            .SingleAsync(b => b.BlogTopicSuggestionId == blogTopicSuggestionId, cancellationToken);

        // Load segments
        var segmentIds = JsonSerializer.Deserialize<List<long>>(blog.SourceSegmentIdsJson ?? "[]") ?? [];
        var segments = await _db.ConversationSegments
            .Where(s => segmentIds.Contains(s.ConversationSegmentId))
            .Select(s => s.ContentText)
            .ToListAsync(cancellationToken);

        var clusterName = blog.ProjectSuggestion?.SuggestedName ?? "Untitled Cluster";

        // Regenerate
        ConsoleLog.Info("Blog", $"Regenerating content for: {blog.Title}");

        var title = await _llm.GenerateBlogTitleAsync(segments, clusterName, cancellationToken);
        var outline = await _llm.GenerateBlogOutlineAsync(segments, title, cancellationToken);
        var content = await _llm.GenerateBlogContentAsync(segments, title, outline, 2000, cancellationToken);
        var wordCount = content.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;

        // Update
        blog.Title = title;
        blog.OutlineJson = JsonSerializer.Serialize(outline);
        blog.BlogContentMarkdown = content;
        blog.WordCount = wordCount;
        blog.GeneratedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(blog);
    }

    public async Task<BlogTopicSuggestionDto> ReEvaluateAsync(
        long blogTopicSuggestionId,
        CancellationToken cancellationToken = default)
    {
        var blog = await _db.BlogTopicSuggestions
            .Include(b => b.ProjectSuggestion)
            .SingleAsync(b => b.BlogTopicSuggestionId == blogTopicSuggestionId, cancellationToken);

        // Load segments
        var segmentIds = JsonSerializer.Deserialize<List<long>>(blog.SourceSegmentIdsJson ?? "[]") ?? [];
        var segments = await _db.ConversationSegments
            .Where(s => segmentIds.Contains(s.ConversationSegmentId))
            .Select(s => s.ContentText)
            .ToListAsync(cancellationToken);

        var clusterName = blog.ProjectSuggestion?.SuggestedName ?? "Untitled Cluster";

        // Re-evaluate only (no content regeneration)
        ConsoleLog.Info("Blog", $"Re-evaluating scores for: {blog.Title}");

        var scores = await _llm.EvaluateClusterForBlogAsync(segments, clusterName, cancellationToken);

        ConsoleLog.Info("Blog", $"  New score: {scores.OverallScore:F2} " +
            $"(Ed:{scores.EducationalValue:F2} PS:{scores.ProblemSolvingDepth:F2} " +
            $"TC:{scores.TopicCoherence:F2} CC:{scores.ContentCompleteness:F2} RI:{scores.ReaderInterest:F2})");

        // Update scores only
        blog.EvaluationScoreJson = JsonSerializer.Serialize(scores);
        blog.Confidence = scores.OverallScore;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(blog);
    }

    public async Task<IReadOnlyList<BlogTopicSuggestionDto>> GetPendingSuggestionsAsync()
    {
        return await GetSuggestionsByStatusAsync("Pending");
    }

    public async Task<IReadOnlyList<BlogTopicSuggestionDto>> GetSuggestionsByStatusAsync(string status)
    {
        var blogs = await _db.BlogTopicSuggestions
            .Include(b => b.ProjectSuggestion)
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.Confidence)
            .ToListAsync();

        return blogs.Select(ToDto).ToList();
    }

    public async Task<BlogTopicSuggestionDetailDto?> GetSuggestionDetailAsync(long id)
    {
        var blog = await _db.BlogTopicSuggestions
            .Include(b => b.ProjectSuggestion)
            .SingleOrDefaultAsync(b => b.BlogTopicSuggestionId == id);

        if (blog == null) return null;

        var scores = ParseScores(blog.EvaluationScoreJson);
        var conversationIds = JsonSerializer.Deserialize<List<long>>(blog.SourceConversationIdsJson) ?? [];
        var segmentIds = JsonSerializer.Deserialize<List<long>>(blog.SourceSegmentIdsJson ?? "[]") ?? [];

        return new BlogTopicSuggestionDetailDto(
            BlogTopicSuggestionId: blog.BlogTopicSuggestionId,
            Title: blog.Title,
            BlogContentMarkdown: blog.BlogContentMarkdown,
            OutlineJson: blog.OutlineJson,
            OverallScore: blog.Confidence,
            EducationalValue: scores.EducationalValue,
            ProblemSolvingDepth: scores.ProblemSolvingDepth,
            TopicCoherence: scores.TopicCoherence,
            ContentCompleteness: scores.ContentCompleteness,
            ReaderInterest: scores.ReaderInterest,
            Reasoning: scores.Reasoning,
            WordCount: blog.WordCount,
            Status: blog.Status,
            GeneratedAtUtc: blog.GeneratedAtUtc,
            ProjectSuggestionId: blog.ProjectSuggestionId,
            ClusterName: blog.ProjectSuggestion?.SuggestedName,
            SourceConversationIds: conversationIds,
            SourceSegmentIds: segmentIds);
    }

    public async Task ApproveAsync(long id)
    {
        var blog = await _db.BlogTopicSuggestions.SingleAsync(b => b.BlogTopicSuggestionId == id);
        blog.Status = "Approved";
        await _db.SaveChangesAsync();
    }

    public async Task DismissAsync(long id)
    {
        var blog = await _db.BlogTopicSuggestions.SingleAsync(b => b.BlogTopicSuggestionId == id);
        blog.Status = "Dismissed";
        await _db.SaveChangesAsync();
    }

    public async Task<string> ExportToMarkdownAsync(long id)
    {
        var blog = await _db.BlogTopicSuggestions.SingleAsync(b => b.BlogTopicSuggestionId == id);

        var markdown = $"""
            # {blog.Title}

            {blog.BlogContentMarkdown}

            ---
            *Generated by ChatLake on {blog.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC*
            *Confidence Score: {blog.Confidence:P0}*
            """;

        return markdown;
    }

    private BlogTopicSuggestionDto ToDto(BlogTopicSuggestion blog)
    {
        var scores = ParseScores(blog.EvaluationScoreJson);

        return new BlogTopicSuggestionDto(
            BlogTopicSuggestionId: blog.BlogTopicSuggestionId,
            Title: blog.Title,
            OverallScore: blog.Confidence,
            EducationalValue: scores.EducationalValue,
            ProblemSolvingDepth: scores.ProblemSolvingDepth,
            TopicCoherence: scores.TopicCoherence,
            ContentCompleteness: scores.ContentCompleteness,
            ReaderInterest: scores.ReaderInterest,
            Reasoning: scores.Reasoning,
            WordCount: blog.WordCount,
            Status: blog.Status,
            GeneratedAtUtc: blog.GeneratedAtUtc,
            ProjectSuggestionId: blog.ProjectSuggestionId,
            ClusterName: blog.ProjectSuggestion?.SuggestedName);
    }

    private static BlogEvaluationScores ParseScores(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new BlogEvaluationScores();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new BlogEvaluationScores
            {
                EducationalValue = GetDecimal(root, "educationalValue") ?? GetDecimal(root, "EducationalValue") ?? 0,
                ProblemSolvingDepth = GetDecimal(root, "problemSolvingDepth") ?? GetDecimal(root, "ProblemSolvingDepth") ?? 0,
                TopicCoherence = GetDecimal(root, "topicCoherence") ?? GetDecimal(root, "TopicCoherence") ?? 0,
                ContentCompleteness = GetDecimal(root, "contentCompleteness") ?? GetDecimal(root, "ContentCompleteness") ?? 0,
                ReaderInterest = GetDecimal(root, "readerInterest") ?? GetDecimal(root, "ReaderInterest") ?? 0,
                Reasoning = root.TryGetProperty("reasoning", out var r) ? r.GetString() :
                    (root.TryGetProperty("Reasoning", out var r2) ? r2.GetString() : null)
            };
        }
        catch
        {
            return new BlogEvaluationScores();
        }
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return (decimal)prop.GetDouble();
        }
        return null;
    }
}
