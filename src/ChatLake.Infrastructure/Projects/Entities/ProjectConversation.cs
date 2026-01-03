namespace ChatLake.Infrastructure.Projects.Entities;

public class ProjectConversation
{
    public long ProjectId { get; set; }
    public long ConversationId { get; set; }

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Manual | System</summary>
    public string AddedBy { get; set; } = "Manual";
}
