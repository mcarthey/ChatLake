namespace ChatLake.Infrastructure.Projects.Entities;

public class ConversationSummary
{
    public long ConversationId { get; set; }

    public int MessageCount { get; set; }

    public DateTime? FirstMessageAtUtc { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }

    /// <summary>Snapshot like "user,assistant"</summary>
    public string ParticipantSet { get; set; } = null!;

    public string PreviewText { get; set; } = null!;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
