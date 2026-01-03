using System;

namespace ChatLake.Infrastructure.Conversations.Entities;

public class ConversationArtifactMap
{
    public long ConversationId { get; set; }
    public long RawArtifactId { get; set; }

    public DateTime MappedAtUtc { get; set; } = DateTime.UtcNow;
}
