namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Maps conversations to topics with relevance scores.
/// </summary>
public class ConversationTopic
{
    public long ConversationTopicId { get; set; }

    public long InferenceRunId { get; set; }
    public InferenceRun InferenceRun { get; set; } = null!;

    public long ConversationId { get; set; }

    public long TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    /// <summary>
    /// Relevance score (0.000000–1.000000)
    /// </summary>
    public decimal Score { get; set; }
}
