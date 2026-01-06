using ChatLake.Infrastructure.Projects.Entities;

namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Inbox item for suggested projects from clustering.
/// Supports human-in-the-loop approval workflow.
/// </summary>
public class ProjectSuggestion
{
    public long ProjectSuggestionId { get; set; }

    public long InferenceRunId { get; set; }
    public InferenceRun InferenceRun { get; set; } = null!;

    /// <summary>
    /// Suggested URL-friendly slug.
    /// </summary>
    public string SuggestedProjectKey { get; set; } = null!;

    public string SuggestedName { get; set; } = null!;

    public string? Summary { get; set; }

    /// <summary>
    /// Confidence score (0.0000–1.0000)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Status: Pending, Accepted, Rejected, Merged
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// JSON array of conversation IDs in this cluster.
    /// Used for backward compatibility with conversation-level clustering.
    /// </summary>
    public string ConversationIdsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of segment IDs in this cluster.
    /// Used for segment-level clustering.
    /// </summary>
    public string? SegmentIdsJson { get; set; }

    /// <summary>
    /// Number of unique conversations represented by segments.
    /// </summary>
    public int UniqueConversationCount { get; set; }

    /// <summary>
    /// If accepted/merged, the resulting Project.
    /// </summary>
    public long? ResolvedProjectId { get; set; }
    public Project? ResolvedProject { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }
}
