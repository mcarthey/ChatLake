namespace ChatLake.Core.Services;

/// <summary>
/// Service for calculating conversation similarity.
/// Powers the "Have I solved this before?" feature.
/// </summary>
public interface ISimilarityService
{
    /// <summary>
    /// Calculate similarity between all conversation pairs.
    /// Only pairs above the threshold are stored.
    /// </summary>
    Task<SimilarityResult> CalculateSimilarityAsync(SimilarityOptions? options = null);

    /// <summary>
    /// Find conversations similar to a given conversation.
    /// </summary>
    Task<IReadOnlyList<SimilarConversationDto>> FindSimilarAsync(long conversationId, int limit = 10);

    /// <summary>
    /// Find conversations similar to a query text (for new questions).
    /// </summary>
    Task<IReadOnlyList<SimilarConversationDto>> SearchSimilarAsync(string queryText, int limit = 10);

    /// <summary>
    /// Get similarity between two specific conversations.
    /// </summary>
    Task<decimal?> GetSimilarityAsync(long conversationIdA, long conversationIdB);
}

/// <summary>
/// Configuration options for similarity calculation.
/// </summary>
public sealed record SimilarityOptions
{
    /// <summary>
    /// Minimum similarity score to store (0.0-1.0). Default is 0.3.
    /// </summary>
    public decimal MinSimilarityThreshold { get; init; } = 0.3m;

    /// <summary>
    /// Maximum number of similar pairs to store per conversation.
    /// </summary>
    public int MaxPairsPerConversation { get; init; } = 20;

    /// <summary>
    /// Method for similarity calculation.
    /// </summary>
    public SimilarityMethod Method { get; init; } = SimilarityMethod.TfidfCosine;

    /// <summary>
    /// Model version string for tracking.
    /// </summary>
    public string ModelVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Similarity calculation method.
/// </summary>
public enum SimilarityMethod
{
    /// <summary>
    /// TF-IDF weighted cosine similarity.
    /// </summary>
    TfidfCosine
}

/// <summary>
/// Result of a similarity calculation operation.
/// </summary>
public sealed record SimilarityResult(
    long InferenceRunId,
    int ConversationsProcessed,
    int PairsCalculated,
    int PairsStored,
    TimeSpan Duration);

/// <summary>
/// A similar conversation with its similarity score.
/// </summary>
public sealed record SimilarConversationDto(
    long ConversationId,
    string? Title,
    string? PreviewText,
    decimal Similarity,
    DateTime? FirstMessageAtUtc,
    int MessageCount);
