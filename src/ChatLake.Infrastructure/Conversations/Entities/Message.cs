using System;

namespace ChatLake.Infrastructure.Conversations.Entities;

public class Message
{
    public long MessageId { get; set; }

    public long ConversationId { get; set; }

    public string Role { get; set; } = null!;
    public int SequenceIndex { get; set; }

    public string Content { get; set; } = null!;
    public byte[] ContentHash { get; set; } = null!; // SHA-256 (binary 32)

    public DateTime? MessageTimestampUtc { get; set; }

    public long RawArtifactId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
