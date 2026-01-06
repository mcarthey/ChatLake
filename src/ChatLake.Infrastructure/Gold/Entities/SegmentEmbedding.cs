using ChatLake.Infrastructure.Conversations.Entities;

namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Stored embedding vector for a conversation segment.
/// Cached to avoid regenerating on each clustering run.
/// </summary>
public class SegmentEmbedding
{
    public long SegmentEmbeddingId { get; set; }

    public long ConversationSegmentId { get; set; }
    public ConversationSegment ConversationSegment { get; set; } = null!;

    public long InferenceRunId { get; set; }
    public InferenceRun InferenceRun { get; set; } = null!;

    /// <summary>
    /// Model used to generate embedding, e.g., "nomic-embed-text"
    /// </summary>
    public string EmbeddingModel { get; set; } = null!;

    /// <summary>
    /// Embedding dimensions (e.g., 768 for nomic-embed-text)
    /// </summary>
    public int Dimensions { get; set; }

    /// <summary>
    /// Binary storage of float[] embedding.
    /// 768 floats * 4 bytes = 3072 bytes per embedding.
    /// </summary>
    public byte[] EmbeddingVector { get; set; } = null!;

    /// <summary>
    /// Hash of source ContentText to detect if re-embedding needed.
    /// </summary>
    public byte[] SourceContentHash { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
