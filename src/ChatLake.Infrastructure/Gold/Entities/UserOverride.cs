namespace ChatLake.Infrastructure.Gold.Entities;

/// <summary>
/// Captures human decisions that must survive inference reruns.
/// Generic event log for all override types.
/// </summary>
public class UserOverride
{
    public long UserOverrideId { get; set; }

    /// <summary>
    /// Type of override: AcceptProjectSuggestion, RejectProjectSuggestion,
    /// ManualProjectAssignment, MergeProjects, SplitProject, RenameProject, SuppressSuggestion
    /// </summary>
    public string OverrideType { get; set; } = null!;

    /// <summary>
    /// Target entity type: Project, ProjectSuggestion, Conversation, Topic, BlogTopicSuggestion
    /// </summary>
    public string TargetType { get; set; } = null!;

    /// <summary>
    /// ID of the target entity.
    /// </summary>
    public long TargetId { get; set; }

    /// <summary>
    /// JSON payload with additional override details.
    /// </summary>
    public string? PayloadJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Username who made the override.
    /// </summary>
    public string? CreatedBy { get; set; }
}
