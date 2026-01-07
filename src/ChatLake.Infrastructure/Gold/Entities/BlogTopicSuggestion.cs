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

    /// <summary>
    /// Link to the ProjectSuggestion (cluster) this blog was generated from.
    /// </summary>
    public long? ProjectSuggestionId { get; set; }
    public ProjectSuggestion? ProjectSuggestion { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// JSON containing outline structure.
    /// </summary>
    public string? OutlineJson { get; set; }

    /// <summary>
    /// Full markdown content of the generated blog post.
    /// </summary>
    public string? BlogContentMarkdown { get; set; }

    /// <summary>
    /// JSON containing evaluation criteria scores.
    /// Example: {"educationalValue":0.85,"problemSolvingDepth":0.72,...}
    /// </summary>
    public string? EvaluationScoreJson { get; set; }

    /// <summary>
    /// Confidence/overall score (0.0000–1.0000)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// JSON array of source conversation IDs.
    /// </summary>
    public string SourceConversationIdsJson { get; set; } = null!;

    /// <summary>
    /// JSON array of source segment IDs used for content generation.
    /// </summary>
    public string? SourceSegmentIdsJson { get; set; }

    /// <summary>
    /// Word count of generated content.
    /// </summary>
    public int? WordCount { get; set; }

    /// <summary>
    /// Status: Pending, Approved, Dismissed
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// When the blog content was generated.
    /// </summary>
    public DateTime? GeneratedAtUtc { get; set; }
}
