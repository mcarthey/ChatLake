namespace ChatLake.Core.Services;

/// <summary>
/// Service for segmenting conversations into topic-coherent units.
/// </summary>
public interface ISegmentationService
{
    /// <summary>
    /// Segment all conversations that haven't been segmented yet.
    /// </summary>
    Task<SegmentationResult> SegmentConversationsAsync(
        SegmentationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get segments for a specific conversation.
    /// </summary>
    Task<IReadOnlyList<SegmentInfo>> GetSegmentsAsync(long conversationId);

    /// <summary>
    /// Get all segments that need embeddings generated.
    /// </summary>
    Task<IReadOnlyList<SegmentInfo>> GetSegmentsWithoutEmbeddingsAsync(
        string embeddingModel = "nomic-embed-text");

    /// <summary>
    /// Clear all segments and embeddings to allow re-segmentation with new options.
    /// </summary>
    Task<int> ResetAllSegmentsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for the segmentation algorithm.
/// </summary>
public sealed record SegmentationOptions
{
    /// <summary>
    /// Window size for embedding comparison (messages).
    /// </summary>
    public int WindowSize { get; init; } = 4;

    /// <summary>
    /// Similarity threshold below which a segment boundary is detected.
    /// Range: 0.0 to 1.0. Lower values create more segments.
    /// </summary>
    public float SimilarityThreshold { get; init; } = 0.55f;

    /// <summary>
    /// Minimum messages per segment.
    /// </summary>
    public int MinSegmentSize { get; init; } = 3;

    /// <summary>
    /// Maximum messages per segment (force split if exceeded).
    /// </summary>
    public int MaxSegmentSize { get; init; } = 50;

    // Conversation filtering options

    /// <summary>
    /// Minimum number of substantive messages required for a conversation to be segmented.
    /// Conversations with fewer messages are skipped (profile-only, trivial Q&amp;A).
    /// </summary>
    public int MinConversationMessages { get; init; } = 3;

    /// <summary>
    /// Minimum total content length (characters) for a conversation to be segmented.
    /// Skips very short conversations even if they have multiple messages.
    /// </summary>
    public int MinContentLength { get; init; } = 200;
}

/// <summary>
/// Result of a segmentation run.
/// </summary>
public sealed record SegmentationResult(
    long InferenceRunId,
    int ConversationsProcessed,
    int SegmentsCreated,
    TimeSpan Duration);

/// <summary>
/// Information about a conversation segment.
/// </summary>
public sealed record SegmentInfo(
    long ConversationSegmentId,
    long ConversationId,
    int SegmentIndex,
    int StartMessageIndex,
    int EndMessageIndex,
    int MessageCount,
    string? ContentPreview);
