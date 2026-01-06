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
/// Uses UMAP dimensionality reduction + HDBSCAN (industry standard approach).
/// </summary>
public sealed record ClusteringOptions
{
    // UMAP parameters (dimensionality reduction)

    /// <summary>
    /// Target dimensionality after UMAP reduction (768D → this).
    /// Lower values = faster but may lose nuance. BERTopic uses 5, we use 15.
    /// </summary>
    public int UmapDimensions { get; init; } = 15;

    /// <summary>
    /// Number of neighbors for UMAP manifold approximation.
    /// Lower = more local structure, higher = more global structure.
    /// </summary>
    public int UmapNeighbors { get; init; } = 15;

    // HDBSCAN parameters (density-based clustering)

    /// <summary>
    /// Minimum number of segments required to form a cluster.
    /// Smaller values create more clusters; larger values create fewer, denser clusters.
    /// </summary>
    public int MinClusterSize { get; init; } = 5;

    /// <summary>
    /// Number of neighboring points used to determine core samples.
    /// Higher values make the algorithm more conservative (fewer clusters, more noise).
    /// </summary>
    public int MinPoints { get; init; } = 3;

    /// <summary>
    /// Random seed for reproducibility.
    /// </summary>
    public int RandomSeed { get; init; } = 42;

    /// <summary>
    /// Minimum confidence threshold for auto-accepting suggestions (0.0-1.0).
    /// Suggestions below this remain pending for manual review.
    /// </summary>
    public decimal AutoAcceptThreshold { get; init; } = 1.1m; // >1.0 disables auto-accept

    /// <summary>
    /// Model version string for tracking.
    /// </summary>
    public string ModelVersion { get; init; } = "3.0.0-umap-hdbscan";
}

/// <summary>
/// Result of a clustering operation.
/// </summary>
public sealed record ClusteringResult(
    long InferenceRunId,
    int SegmentCount,
    int ConversationCount,
    int ClusterCount,
    int SuggestionsCreated,
    int NoiseCount,
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
