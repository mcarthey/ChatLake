namespace ChatLake.Core.Services;

/// <summary>
/// Standardized tracking for ML/inference operations.
/// All derived data should be traceable to an InferenceRun.
/// </summary>
public interface IInferenceRunService
{
    /// <summary>
    /// Start a new inference run. Returns the run ID.
    /// </summary>
    /// <param name="runType">Type: Clustering, Topics, Similarity, Drift, BlogTopics</param>
    /// <param name="modelName">Model identifier, e.g., "ChatLake.KMeans.v1"</param>
    /// <param name="modelVersion">Semantic version of the model</param>
    /// <param name="inputScope">Scope: All, ImportBatchRange, Project, ConversationSet</param>
    /// <param name="featureConfigHash">SHA-256 hash of feature extraction config for reproducibility</param>
    /// <param name="inputDescription">Optional description of input data</param>
    Task<long> StartRunAsync(
        string runType,
        string modelName,
        string modelVersion,
        string inputScope,
        byte[] featureConfigHash,
        string? inputDescription = null);

    /// <summary>
    /// Mark a run as successfully completed.
    /// </summary>
    /// <param name="runId">The run ID from StartRunAsync</param>
    /// <param name="metricsJson">Optional JSON metrics (silhouette score, etc.)</param>
    Task CompleteRunAsync(long runId, string? metricsJson = null);

    /// <summary>
    /// Mark a run as failed.
    /// </summary>
    /// <param name="runId">The run ID from StartRunAsync</param>
    /// <param name="errorMessage">Error description</param>
    Task FailRunAsync(long runId, string errorMessage);

    /// <summary>
    /// Get a specific run by ID.
    /// </summary>
    Task<InferenceRunDto?> GetRunAsync(long runId);

    /// <summary>
    /// Get recent runs, optionally filtered by type.
    /// </summary>
    Task<IReadOnlyList<InferenceRunDto>> GetRecentRunsAsync(string? runType = null, int limit = 20);
}

public sealed record InferenceRunDto(
    long InferenceRunId,
    string RunType,
    string ModelName,
    string ModelVersion,
    string InputScope,
    string? InputDescription,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string Status,
    string? MetricsJson);
