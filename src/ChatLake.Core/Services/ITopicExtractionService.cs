namespace ChatLake.Core.Services;

/// <summary>
/// Service for extracting topics from conversations using LDA.
/// </summary>
public interface ITopicExtractionService
{
    /// <summary>
    /// Extract topics from all conversations.
    /// Creates Topic and ConversationTopic records.
    /// </summary>
    /// <param name="options">Topic extraction configuration</param>
    /// <returns>Result containing run ID and topic count</returns>
    Task<TopicExtractionResult> ExtractTopicsAsync(TopicExtractionOptions? options = null);

    /// <summary>
    /// Get topics for a specific inference run.
    /// </summary>
    Task<IReadOnlyList<TopicDto>> GetTopicsAsync(long inferenceRunId);

    /// <summary>
    /// Get topic assignments for a conversation.
    /// </summary>
    Task<IReadOnlyList<ConversationTopicDto>> GetConversationTopicsAsync(long conversationId);
}

/// <summary>
/// Configuration options for topic extraction.
/// </summary>
public sealed record TopicExtractionOptions
{
    /// <summary>
    /// Number of topics to extract. Default is 10.
    /// </summary>
    public int TopicCount { get; init; } = 10;

    /// <summary>
    /// Number of top keywords to extract per topic. Default is 10.
    /// </summary>
    public int KeywordsPerTopic { get; init; } = 10;

    /// <summary>
    /// Minimum score threshold for topic assignments (0.0-1.0).
    /// Assignments below this threshold are not saved.
    /// </summary>
    public decimal MinScoreThreshold { get; init; } = 0.05m;

    /// <summary>
    /// Model version string for tracking.
    /// </summary>
    public string ModelVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Result of a topic extraction operation.
/// </summary>
public sealed record TopicExtractionResult(
    long InferenceRunId,
    int ConversationCount,
    int TopicCount,
    int AssignmentsCreated,
    TimeSpan Duration);

/// <summary>
/// Topic with its keywords.
/// </summary>
public sealed record TopicDto(
    long TopicId,
    string Label,
    IReadOnlyList<string> Keywords,
    int ConversationCount);

/// <summary>
/// Topic assignment for a conversation.
/// </summary>
public sealed record ConversationTopicDto(
    long TopicId,
    string TopicLabel,
    decimal Score);
