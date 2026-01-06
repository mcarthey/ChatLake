using ChatLake.Infrastructure.Gold.Entities;

namespace ChatLake.Infrastructure.Conversations.Entities;

/// <summary>
/// A topic-coherent segment within a conversation.
/// Segments partition a conversation into clusterable units based on topic boundaries.
/// </summary>
public class ConversationSegment
{
    public long ConversationSegmentId { get; set; }

    public long ConversationId { get; set; }

    /// <summary>
    /// Zero-based index of this segment within the conversation.
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// Starting message SequenceIndex (inclusive).
    /// </summary>
    public int StartMessageIndex { get; set; }

    /// <summary>
    /// Ending message SequenceIndex (inclusive).
    /// </summary>
    public int EndMessageIndex { get; set; }

    /// <summary>
    /// Number of messages in this segment.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Concatenated text of all messages in segment.
    /// Enables embedding without re-querying messages.
    /// </summary>
    public string ContentText { get; set; } = null!;

    /// <summary>
    /// SHA-256 hash of ContentText for change detection.
    /// </summary>
    public byte[] ContentHash { get; set; } = null!;

    /// <summary>
    /// InferenceRunId that created this segmentation.
    /// </summary>
    public long InferenceRunId { get; set; }
    public InferenceRun InferenceRun { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
