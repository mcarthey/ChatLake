namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Similarity edges between conversations for "Have I solved this before?" feature.
/// ConversationIdA is always less than ConversationIdB to prevent duplicates.
/// </summary>
public class ConversationSimilarity
{
    public long ConversationSimilarityId { get; set; }

    public long InferenceRunId { get; set; }
    public InferenceRun InferenceRun { get; set; } = null!;

    /// <summary>
    /// First conversation (always the lower ID).
    /// </summary>
    public long ConversationIdA { get; set; }

    /// <summary>
    /// Second conversation (always the higher ID).
    /// </summary>
    public long ConversationIdB { get; set; }

    /// <summary>
    /// Similarity score (0.000000–1.000000)
    /// </summary>
    public decimal Similarity { get; set; }

    /// <summary>
    /// Method used: TfidfCosine, EmbeddingCosine
    /// </summary>
    public string Method { get; set; } = null!;
}
