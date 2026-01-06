using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

/// <summary>
/// Service for detecting topic drift in projects over time.
/// </summary>
public sealed class DriftDetectionService : IDriftDetectionService
{
    private readonly ChatLakeDbContext _db;
    private readonly IInferenceRunService _inferenceRuns;

    private const string ModelName = "ChatLake.TopicDrift";
    private const decimal HighDriftThreshold = 0.3m;

    public DriftDetectionService(ChatLakeDbContext db, IInferenceRunService inferenceRuns)
    {
        _db = db;
        _inferenceRuns = inferenceRuns;
    }

    public async Task<DriftDetectionResult> CalculateDriftAsync(DriftDetectionOptions? options = null)
    {
        options ??= new DriftDetectionOptions();
        var stopwatch = Stopwatch.StartNew();

        // Get all active projects
        var projects = await _db.Projects
            .Where(p => p.IsActive)
            .Select(p => p.ProjectId)
            .ToListAsync();

        if (projects.Count == 0)
        {
            return new DriftDetectionResult(
                InferenceRunId: 0,
                ProjectsAnalyzed: 0,
                MetricsCreated: 0,
                HighDriftCount: 0,
                Duration: stopwatch.Elapsed);
        }

        var configHash = ComputeConfigHash(options);
        var runId = await _inferenceRuns.StartRunAsync(
            runType: "Drift",
            modelName: ModelName,
            modelVersion: options.ModelVersion,
            inputScope: "All",
            featureConfigHash: configHash,
            inputDescription: $"Calculating drift for {projects.Count} projects");

        try
        {
            var totalMetrics = 0;
            var highDriftCount = 0;

            foreach (var projectId in projects)
            {
                var (metrics, highDrift) = await CalculateProjectDriftInternalAsync(runId, projectId, options);
                totalMetrics += metrics;
                highDriftCount += highDrift;
            }

            var metricsJson = JsonSerializer.Serialize(new
            {
                projectsAnalyzed = projects.Count,
                metricsCreated = totalMetrics,
                highDriftCount
            });

            await _inferenceRuns.CompleteRunAsync(runId, metricsJson);
            stopwatch.Stop();

            return new DriftDetectionResult(
                InferenceRunId: runId,
                ProjectsAnalyzed: projects.Count,
                MetricsCreated: totalMetrics,
                HighDriftCount: highDriftCount,
                Duration: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            await _inferenceRuns.FailRunAsync(runId, ex.Message);
            throw;
        }
    }

    public async Task<DriftDetectionResult> CalculateProjectDriftAsync(long projectId, DriftDetectionOptions? options = null)
    {
        options ??= new DriftDetectionOptions();
        var stopwatch = Stopwatch.StartNew();

        var configHash = ComputeConfigHash(options);
        var runId = await _inferenceRuns.StartRunAsync(
            runType: "Drift",
            modelName: ModelName,
            modelVersion: options.ModelVersion,
            inputScope: "Project",
            featureConfigHash: configHash,
            inputDescription: $"Calculating drift for project {projectId}");

        try
        {
            var (metrics, highDrift) = await CalculateProjectDriftInternalAsync(runId, projectId, options);

            var metricsJson = JsonSerializer.Serialize(new
            {
                projectId,
                metricsCreated = metrics,
                highDriftCount = highDrift
            });

            await _inferenceRuns.CompleteRunAsync(runId, metricsJson);
            stopwatch.Stop();

            return new DriftDetectionResult(
                InferenceRunId: runId,
                ProjectsAnalyzed: 1,
                MetricsCreated: metrics,
                HighDriftCount: highDrift,
                Duration: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            await _inferenceRuns.FailRunAsync(runId, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<ProjectDriftMetricDto>> GetProjectDriftAsync(long projectId)
    {
        var metrics = await _db.ProjectDriftMetrics
            .Where(m => m.ProjectId == projectId)
            .OrderByDescending(m => m.WindowEndUtc)
            .ToListAsync();

        return metrics.Select(m => new ProjectDriftMetricDto(
            ProjectDriftMetricId: m.ProjectDriftMetricId,
            WindowStartUtc: m.WindowStartUtc,
            WindowEndUtc: m.WindowEndUtc,
            DriftScore: m.DriftScore,
            TopicShifts: ParseTopicShifts(m.DetailsJson)
        )).ToList();
    }

    public async Task<IReadOnlyList<ProjectDriftSummaryDto>> GetHighDriftProjectsAsync(int limit = 10)
    {
        // Load all drift metrics and group in memory (EF Core can't translate complex GroupBy)
        var allMetrics = await _db.ProjectDriftMetrics.ToListAsync();

        if (!allMetrics.Any())
            return [];

        var groupedMetrics = allMetrics
            .GroupBy(m => m.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                LatestMetric = g.OrderByDescending(m => m.WindowEndUtc).First(),
                AverageScore = g.Average(m => m.DriftScore),
                WindowCount = g.Count()
            })
            .OrderByDescending(x => x.LatestMetric.DriftScore)
            .Take(limit)
            .ToList();

        var projectIds = groupedMetrics.Select(m => m.ProjectId).ToList();
        var projects = await _db.Projects
            .Where(p => projectIds.Contains(p.ProjectId))
            .ToDictionaryAsync(p => p.ProjectId, p => p.Name);

        return groupedMetrics.Select(m => new ProjectDriftSummaryDto(
            ProjectId: m.ProjectId,
            ProjectName: projects.GetValueOrDefault(m.ProjectId, "Unknown"),
            LatestDriftScore: m.LatestMetric.DriftScore,
            AverageDriftScore: m.AverageScore,
            WindowCount: m.WindowCount,
            LastWindowEndUtc: m.LatestMetric.WindowEndUtc
        )).ToList();
    }

    private async Task<(int metricsCreated, int highDriftCount)> CalculateProjectDriftInternalAsync(
        long runId,
        long projectId,
        DriftDetectionOptions options)
    {
        // Get conversations for this project with their timestamps
        var conversationIds = await _db.ProjectConversations
            .Where(pc => pc.ProjectId == projectId && pc.IsCurrent)
            .Select(pc => pc.ConversationId)
            .ToListAsync();

        if (conversationIds.Count == 0)
            return (0, 0);

        // Get conversation timestamps from summaries
        var conversationTimestamps = await _db.ConversationSummaries
            .Where(cs => conversationIds.Contains(cs.ConversationId))
            .Select(cs => new { cs.ConversationId, cs.FirstMessageAtUtc })
            .ToDictionaryAsync(x => x.ConversationId, x => x.FirstMessageAtUtc);

        // Get topic assignments for these conversations
        var topicAssignments = await _db.ConversationTopics
            .Where(ct => conversationIds.Contains(ct.ConversationId))
            .Select(ct => new TopicAssignmentData
            {
                ConversationId = ct.ConversationId,
                TopicId = ct.TopicId,
                Score = ct.Score,
                TopicLabel = ct.Topic.Label
            })
            .ToListAsync();

        if (topicAssignments.Count == 0)
            return (0, 0);

        // Build time windows
        var cutoffDate = DateTime.UtcNow.AddDays(-options.LookbackDays);
        var windows = BuildTimeWindows(
            conversationTimestamps.Values.Where(d => d.HasValue).Select(d => d!.Value),
            cutoffDate,
            options.WindowSizeDays);

        if (windows.Count < 2)
            return (0, 0);

        // Calculate topic distribution for each window
        var windowDistributions = new List<(DateTime start, DateTime end, Dictionary<long, decimal> distribution)>();

        foreach (var (windowStart, windowEnd) in windows)
        {
            var windowConvIds = conversationTimestamps
                .Where(kv => kv.Value >= windowStart && kv.Value < windowEnd)
                .Select(kv => kv.Key)
                .ToHashSet();

            if (windowConvIds.Count < options.MinConversationsPerWindow)
                continue;

            var distribution = CalculateTopicDistribution(topicAssignments
                .Where(ta => windowConvIds.Contains(ta.ConversationId))
                .ToList());

            if (distribution.Count > 0)
            {
                windowDistributions.Add((windowStart, windowEnd, distribution));
            }
        }

        if (windowDistributions.Count < 2)
            return (0, 0);

        // Calculate drift between consecutive windows
        var metricsCreated = 0;
        var highDriftCount = 0;

        for (int i = 1; i < windowDistributions.Count; i++)
        {
            var prev = windowDistributions[i - 1];
            var curr = windowDistributions[i];

            var driftScore = CalculateDriftScore(prev.distribution, curr.distribution);
            var topicShifts = CalculateTopicShifts(prev.distribution, curr.distribution, topicAssignments);

            var metric = new ProjectDriftMetric
            {
                InferenceRunId = runId,
                ProjectId = projectId,
                WindowStartUtc = curr.start,
                WindowEndUtc = curr.end,
                DriftScore = driftScore,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    previousWindow = new { start = prev.start, end = prev.end },
                    topicShifts = topicShifts.Select(ts => new
                    {
                        topicLabel = ts.TopicLabel,
                        previousScore = ts.PreviousScore,
                        currentScore = ts.CurrentScore,
                        change = ts.Change
                    })
                })
            };

            _db.ProjectDriftMetrics.Add(metric);
            metricsCreated++;

            if (driftScore >= HighDriftThreshold)
                highDriftCount++;
        }

        await _db.SaveChangesAsync();
        return (metricsCreated, highDriftCount);
    }

    private sealed class TopicAssignmentData
    {
        public long ConversationId { get; set; }
        public long TopicId { get; set; }
        public decimal Score { get; set; }
        public string TopicLabel { get; set; } = "";
    }

    private static List<(DateTime start, DateTime end)> BuildTimeWindows(
        IEnumerable<DateTime> timestamps,
        DateTime cutoffDate,
        int windowSizeDays)
    {
        var sorted = timestamps.Where(t => t >= cutoffDate).OrderBy(t => t).ToList();
        if (sorted.Count == 0)
            return [];

        var windows = new List<(DateTime start, DateTime end)>();
        var windowStart = sorted.First().Date;
        var now = DateTime.UtcNow;

        while (windowStart < now)
        {
            var windowEnd = windowStart.AddDays(windowSizeDays);
            windows.Add((windowStart, windowEnd));
            windowStart = windowEnd;
        }

        return windows;
    }

    private static Dictionary<long, decimal> CalculateTopicDistribution(
        List<TopicAssignmentData> assignments)
    {
        var topicScores = new Dictionary<long, List<decimal>>();

        foreach (var assignment in assignments)
        {
            if (!topicScores.ContainsKey(assignment.TopicId))
                topicScores[assignment.TopicId] = [];

            topicScores[assignment.TopicId].Add(assignment.Score);
        }

        // Average score per topic
        return topicScores.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Average());
    }

    private static decimal CalculateDriftScore(
        Dictionary<long, decimal> prev,
        Dictionary<long, decimal> curr)
    {
        // Use Jensen-Shannon divergence (symmetric KL divergence)
        // Simplified: use cosine distance
        var allTopics = prev.Keys.Union(curr.Keys).ToList();

        if (allTopics.Count == 0)
            return 0m;

        var prevVec = allTopics.Select(t => (double)prev.GetValueOrDefault(t, 0m)).ToArray();
        var currVec = allTopics.Select(t => (double)curr.GetValueOrDefault(t, 0m)).ToArray();

        // Normalize vectors
        var prevNorm = Math.Sqrt(prevVec.Sum(x => x * x));
        var currNorm = Math.Sqrt(currVec.Sum(x => x * x));

        if (prevNorm == 0 || currNorm == 0)
            return 1m; // Maximum drift if one vector is zero

        for (int i = 0; i < prevVec.Length; i++)
        {
            prevVec[i] /= prevNorm;
            currVec[i] /= currNorm;
        }

        // Cosine similarity
        var dotProduct = prevVec.Zip(currVec, (a, b) => a * b).Sum();

        // Convert to distance (1 - similarity), clamped to [0, 1]
        var distance = 1.0 - dotProduct;
        return (decimal)Math.Max(0, Math.Min(1, distance));
    }

    private static List<TopicShiftDto> CalculateTopicShifts(
        Dictionary<long, decimal> prev,
        Dictionary<long, decimal> curr,
        List<TopicAssignmentData> allAssignments)
    {
        var topicLabels = allAssignments
            .GroupBy(a => a.TopicId)
            .ToDictionary(g => g.Key, g => g.First().TopicLabel);

        var allTopics = prev.Keys.Union(curr.Keys).ToList();

        return allTopics
            .Select(topicId =>
            {
                var prevScore = prev.GetValueOrDefault(topicId, 0m);
                var currScore = curr.GetValueOrDefault(topicId, 0m);
                return new TopicShiftDto(
                    TopicLabel: topicLabels.GetValueOrDefault(topicId, $"Topic {topicId}"),
                    PreviousScore: Math.Round(prevScore, 4),
                    CurrentScore: Math.Round(currScore, 4),
                    Change: Math.Round(currScore - prevScore, 4));
            })
            .OrderByDescending(ts => Math.Abs(ts.Change))
            .ToList();
    }

    private static IReadOnlyList<TopicShiftDto> ParseTopicShifts(string? detailsJson)
    {
        if (string.IsNullOrEmpty(detailsJson))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (!doc.RootElement.TryGetProperty("topicShifts", out var shiftsElement))
                return [];

            var shifts = new List<TopicShiftDto>();
            foreach (var shift in shiftsElement.EnumerateArray())
            {
                shifts.Add(new TopicShiftDto(
                    TopicLabel: shift.GetProperty("topicLabel").GetString() ?? "",
                    PreviousScore: shift.GetProperty("previousScore").GetDecimal(),
                    CurrentScore: shift.GetProperty("currentScore").GetDecimal(),
                    Change: shift.GetProperty("change").GetDecimal()));
            }
            return shifts;
        }
        catch
        {
            return [];
        }
    }

    private static byte[] ComputeConfigHash(DriftDetectionOptions options)
    {
        var config = JsonSerializer.Serialize(new
        {
            modelName = ModelName,
            modelVersion = options.ModelVersion,
            windowSizeDays = options.WindowSizeDays,
            minConversationsPerWindow = options.MinConversationsPerWindow,
            lookbackDays = options.LookbackDays
        });

        return SHA256.HashData(Encoding.UTF8.GetBytes(config));
    }
}
