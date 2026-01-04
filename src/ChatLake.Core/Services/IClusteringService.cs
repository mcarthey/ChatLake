namespace ChatLake.Core.Services;

/// <summary>
/// Service for clustering conversations into suggested projects.
/// </summary>
public interface IClusteringService
{
    /// <summary>
    /// Run clustering on all ungrouped conversations.
    /// Creates ProjectSuggestion records for each discovered cluster.
    /// </summary>
    /// <param name="options">Clustering configuration options</param>
    /// <returns>Result containing run ID and cluster count</returns>
    Task<ClusteringResult> ClusterConversationsAsync(ClusteringOptions? options = null);

    /// <summary>
    /// Get clustering results for a specific inference run.
    /// </summary>
    Task<IReadOnlyList<ClusterSummary>> GetClusterSummariesAsync(long inferenceRunId);
}

/// <summary>
/// Configuration options for clustering.
/// </summary>
public sealed record ClusteringOptions
{
    /// <summary>
    /// Number of clusters to create. If null, uses sqrt(n/2) heuristic.
    /// </summary>
    public int? ClusterCount { get; init; }

    /// <summary>
    /// Minimum confidence threshold for auto-accepting suggestions (0.0-1.0).
    /// Suggestions below this remain pending for manual review.
    /// </summary>
    public decimal AutoAcceptThreshold { get; init; } = 0.8m;

    /// <summary>
    /// Maximum iterations for KMeans algorithm.
    /// </summary>
    public int MaxIterations { get; init; } = 100;

    /// <summary>
    /// Model version string for tracking.
    /// </summary>
    public string ModelVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Result of a clustering operation.
/// </summary>
public sealed record ClusteringResult(
    long InferenceRunId,
    int ConversationCount,
    int ClusterCount,
    int SuggestionsCreated,
    TimeSpan Duration);

/// <summary>
/// Summary of a single cluster.
/// </summary>
public sealed record ClusterSummary(
    long ProjectSuggestionId,
    string SuggestedName,
    string SuggestedProjectKey,
    int ConversationCount,
    decimal Confidence,
    string Status,
    IReadOnlyList<long> SampleConversationIds);
