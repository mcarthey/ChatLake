namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Tracks each ML/derived computation execution.
/// All inference writes are traceable to a run.
/// </summary>
public class InferenceRun
{
    public long InferenceRunId { get; set; }

    /// <summary>
    /// Type of inference: Clustering, Topics, Embeddings, Similarity, Drift, BlogTopics
    /// </summary>
    public string RunType { get; set; } = null!;

    /// <summary>
    /// Model identifier, e.g., "ChatLake.KMeans.v1"
    /// </summary>
    public string ModelName { get; set; } = null!;

    public string ModelVersion { get; set; } = null!;

    /// <summary>
    /// SHA-256 hash of feature extraction configuration for reproducibility.
    /// </summary>
    public byte[] FeatureConfigHashSha256 { get; set; } = null!;

    /// <summary>
    /// Scope of input data: All, ImportBatchRange, Project, ConversationSet
    /// </summary>
    public string InputScope { get; set; } = null!;

    public string? InputDescription { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Status: Running, Completed, Failed
    /// </summary>
    public string Status { get; set; } = "Running";

    /// <summary>
    /// JSON containing metrics like silhouette score, etc.
    /// </summary>
    public string? MetricsJson { get; set; }
}
