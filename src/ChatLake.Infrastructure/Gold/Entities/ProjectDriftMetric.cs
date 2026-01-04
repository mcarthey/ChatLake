using ChatLake.Infrastructure.Projects.Entities;

namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Topic drift/creep metrics for a project over time.
/// </summary>
public class ProjectDriftMetric
{
    public long ProjectDriftMetricId { get; set; }

    public long InferenceRunId { get; set; }
    public InferenceRun InferenceRun { get; set; } = null!;

    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }

    /// <summary>
    /// Drift score (0.000000–1.000000)
    /// </summary>
    public decimal DriftScore { get; set; }

    /// <summary>
    /// JSON with detailed drift analysis.
    /// </summary>
    public string? DetailsJson { get; set; }
}
