namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Extracted topic from ML analysis.
/// </summary>
public class Topic
{
    public long TopicId { get; set; }

    public long InferenceRunId { get; set; }
    public InferenceRun InferenceRun { get; set; } = null!;

    /// <summary>
    /// Human-readable label, e.g., "Klipper", "EF Core", "Act II Draft"
    /// </summary>
    public string Label { get; set; } = null!;

    /// <summary>
    /// JSON array of keywords associated with this topic.
    /// </summary>
    public string? KeywordsJson { get; set; }
}
