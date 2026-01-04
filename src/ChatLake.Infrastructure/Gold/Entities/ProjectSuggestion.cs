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
    /// If accepted/merged, the resulting Project.
    /// </summary>
    public long? ResolvedProjectId { get; set; }
    public Project? ResolvedProject { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }
}
