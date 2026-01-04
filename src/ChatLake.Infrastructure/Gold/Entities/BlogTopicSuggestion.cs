using ChatLake.Infrastructure.Projects.Entities;

namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Suggested blog topics based on research arcs in conversations.
/// </summary>
public class BlogTopicSuggestion
{
    public long BlogTopicSuggestionId { get; set; }

    public long InferenceRunId { get; set; }
    public InferenceRun InferenceRun { get; set; } = null!;

    /// <summary>
    /// Optional associated project.
    /// </summary>
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// JSON containing outline structure.
    /// </summary>
    public string? OutlineJson { get; set; }

    /// <summary>
    /// Confidence score (0.0000–1.0000)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// JSON array of source conversation IDs.
    /// </summary>
    public string SourceConversationIdsJson { get; set; } = null!;

    /// <summary>
    /// Status: Pending, Approved, Dismissed
    /// </summary>
    public string Status { get; set; } = "Pending";
}
