namespace ChatLake.Core.Services;

/// <summary>
/// Service for caching and retrieving segment embeddings.
/// </summary>
public interface IEmbeddingCacheService
{
    /// <summary>
    /// Get or generate embedding for a segment.
    /// Uses cached embedding if ContentHash matches.
    /// </summary>
    Task<float[]?> GetOrGenerateAsync(
        long segmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch generate embeddings for multiple segments.
    /// Only generates for segments without cached embeddings.
    /// </summary>
    Task<EmbeddingGenerationResult> GenerateMissingEmbeddingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all segment embeddings for clustering.
    /// </summary>
    Task<IReadOnlyList<SegmentEmbeddingData>> GetAllEmbeddingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cached embeddings for segments with changed content.
    /// </summary>
    Task<int> InvalidateStaleEmbeddingsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of embedding generation.
/// </summary>
public sealed record EmbeddingGenerationResult(
    long InferenceRunId,
    int SegmentsProcessed,
    int EmbeddingsGenerated,
    int EmbeddingsCached,
    TimeSpan Duration);

/// <summary>
/// Segment embedding data for clustering.
/// </summary>
public sealed record SegmentEmbeddingData(
    long ConversationSegmentId,
    long ConversationId,
    float[] Embedding);
