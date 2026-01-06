namespace ChatLake.Core.Services;

/// <summary>
/// Service for interacting with a local LLM (e.g., Ollama).
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Generate a short project name for a cluster of conversations.
    /// </summary>
    /// <param name="sampleTexts">Sample conversation snippets from the cluster</param>
    /// <param name="conversationCount">Total conversations in the cluster</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A short descriptive name (2-5 words)</returns>
    Task<string> GenerateClusterNameAsync(
        IReadOnlyList<string> sampleTexts,
        int conversationCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate semantic embeddings for text.
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector, or null if embeddings not available</returns>
    Task<float[]?> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the LLM service is available.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if embedding generation is available.
    /// </summary>
    Task<bool> IsEmbeddingAvailableAsync(CancellationToken cancellationToken = default);
}
