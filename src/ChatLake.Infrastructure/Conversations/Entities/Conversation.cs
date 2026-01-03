using System;

namespace ChatLake.Infrastructure.Conversations.Entities;

public class Conversation
{
    public long ConversationId { get; set; }

    /// <summary>
    /// Deterministic SHA-256 hash (binary 32) of ordered (Role + ContentHash) list.
    /// </summary>
    public byte[] ConversationKey { get; set; } = null!;

    public string SourceSystem { get; set; } = null!;
    public string? ExternalConversationId { get; set; }

    public DateTime? FirstMessageAtUtc { get; set; }
    public DateTime? LastMessageAtUtc { get; set; }

    public long CreatedFromImportBatchId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
