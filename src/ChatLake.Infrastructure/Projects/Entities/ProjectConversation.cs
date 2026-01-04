using ChatLake.Infrastructure.Gold.Entities;

namespace ChatLake.Infrastructure.Projects.Entities;

/// <summary>
/// Maps conversations to projects with assignment metadata.
/// Supports history when reruns occur via IsCurrent flag.
/// </summary>
public class ProjectConversation
{
    public long ProjectConversationId { get; set; }

    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public long ConversationId { get; set; }

    /// <summary>
    /// Who assigned: System or User
    /// </summary>
    public string AssignedBy { get; set; } = "User";

    /// <summary>
    /// If system-assigned, which inference run created this.
    /// </summary>
    public long? InferenceRunId { get; set; }
    public InferenceRun? InferenceRun { get; set; }

    /// <summary>
    /// Confidence score for system assignments (0.0000–1.0000)
    /// </summary>
    public decimal? Confidence { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Supports history: only one current assignment per project+conversation.
    /// </summary>
    public bool IsCurrent { get; set; } = true;
}
