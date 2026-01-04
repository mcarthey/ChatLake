namespace ChatLake.Core.Services;

/// <summary>
/// Service for detecting topic drift in projects over time.
/// Drift measures how much a project's topic focus has shifted between time windows.
/// </summary>
public interface IDriftDetectionService
{
    /// <summary>
    /// Calculate drift metrics for all projects.
    /// Compares topic distributions across consecutive time windows.
    /// </summary>
    /// <param name="options">Drift detection configuration</param>
    /// <returns>Result containing run ID and metrics created</returns>
    Task<DriftDetectionResult> CalculateDriftAsync(DriftDetectionOptions? options = null);

    /// <summary>
    /// Calculate drift metrics for a specific project.
    /// </summary>
    Task<DriftDetectionResult> CalculateProjectDriftAsync(long projectId, DriftDetectionOptions? options = null);

    /// <summary>
    /// Get drift metrics for a project.
    /// </summary>
    Task<IReadOnlyList<ProjectDriftMetricDto>> GetProjectDriftAsync(long projectId);

    /// <summary>
    /// Get projects with highest drift scores.
    /// </summary>
    Task<IReadOnlyList<ProjectDriftSummaryDto>> GetHighDriftProjectsAsync(int limit = 10);
}

/// <summary>
/// Configuration options for drift detection.
/// </summary>
public sealed record DriftDetectionOptions
{
    /// <summary>
    /// Size of each time window in days. Default is 30 days.
    /// </summary>
    public int WindowSizeDays { get; init; } = 30;

    /// <summary>
    /// Minimum number of conversations required per window to calculate drift.
    /// </summary>
    public int MinConversationsPerWindow { get; init; } = 3;

    /// <summary>
    /// How far back to analyze. Default is 365 days.
    /// </summary>
    public int LookbackDays { get; init; } = 365;

    /// <summary>
    /// Model version string for tracking.
    /// </summary>
    public string ModelVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Result of a drift detection operation.
/// </summary>
public sealed record DriftDetectionResult(
    long InferenceRunId,
    int ProjectsAnalyzed,
    int MetricsCreated,
    int HighDriftCount,
    TimeSpan Duration);

/// <summary>
/// Drift metric for a time window.
/// </summary>
public sealed record ProjectDriftMetricDto(
    long ProjectDriftMetricId,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    decimal DriftScore,
    IReadOnlyList<TopicShiftDto> TopicShifts);

/// <summary>
/// Topic shift between windows.
/// </summary>
public sealed record TopicShiftDto(
    string TopicLabel,
    decimal PreviousScore,
    decimal CurrentScore,
    decimal Change);

/// <summary>
/// Summary of drift for a project.
/// </summary>
public sealed record ProjectDriftSummaryDto(
    long ProjectId,
    string ProjectName,
    decimal LatestDriftScore,
    decimal AverageDriftScore,
    int WindowCount,
    DateTime? LastWindowEndUtc);
