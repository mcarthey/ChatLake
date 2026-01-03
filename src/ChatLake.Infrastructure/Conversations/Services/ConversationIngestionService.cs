using System.Security.Cryptography;
using System.Text;
using ChatLake.Core.Parsing;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Conversations.Services;

/// <summary>
/// Canonical persistence for parsed conversations and messages.
/// Idempotency is enforced by database constraints.
/// </summary>
public sealed class ConversationIngestionService : IConversationIngestionService
{
    private readonly ChatLakeDbContext _db;

    public ConversationIngestionService(ChatLakeDbContext db)
    {
        _db = db;
    }

    public async Task IngestAsync(
        long importBatchId,
        long rawArtifactId,
        IReadOnlyCollection<ParsedConversation> parsedConversations)
    {
        foreach (var parsed in parsedConversations)
        {
            var conversationKey = ComputeConversationKey(parsed);

            var conversation = await _db.Conversations
                .SingleOrDefaultAsync(c => c.ConversationKey == conversationKey);

            if (conversation is null)
            {
                conversation = new Conversation
                {
                    ConversationKey = conversationKey,
                    SourceSystem = parsed.SourceSystem,
                    ExternalConversationId = parsed.ExternalConversationId,
                    CreatedFromImportBatchId = importBatchId,
                    FirstMessageAtUtc = parsed.Messages.FirstOrDefault()?.MessageTimestampUtc,
                    LastMessageAtUtc = parsed.Messages.LastOrDefault()?.MessageTimestampUtc,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.Conversations.Add(conversation);
                await _db.SaveChangesAsync();
            }

            // Provenance (idempotent via composite PK)
            _db.ConversationArtifactMaps.Add(new ConversationArtifactMap
            {
                ConversationId = conversation.ConversationId,
                RawArtifactId = rawArtifactId
            });

            foreach (var msg in parsed.Messages)
            {
                _db.Messages.Add(new Message
                {
                    ConversationId = conversation.ConversationId,
                    Role = msg.Role,
                    SequenceIndex = msg.SequenceIndex,
                    Content = msg.Content,
                    ContentHash = ComputeSha256(msg.Content),
                    MessageTimestampUtc = msg.MessageTimestampUtc,
                    RawArtifactId = rawArtifactId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Expected when unique constraints reject duplicates.
                // This is the idempotency mechanism.
            }
        }
    }

    private static byte[] ComputeConversationKey(ParsedConversation parsed)
    {
        using var sha = SHA256.Create();

        foreach (var msg in parsed.Messages)
        {
            var bytes = Encoding.UTF8.GetBytes(msg.Role + msg.Content);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return sha.Hash!;
    }

    private static byte[] ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(content));
    }
}
