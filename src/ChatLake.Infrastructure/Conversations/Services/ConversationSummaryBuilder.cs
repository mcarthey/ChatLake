using ChatLake.Core.Services;
using ChatLake.Infrastructure.Persistence;
using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Conversations.Services;

public sealed class ConversationSummaryBuilder : IConversationSummaryBuilder
{
    private readonly ChatLakeDbContext _db;

    public ConversationSummaryBuilder(ChatLakeDbContext db)
    {
        _db = db;
    }

    public async Task RebuildAsync(long conversationId)
    {
        var messages = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SequenceIndex)
            .ToListAsync();

        if (messages.Count == 0)
            return;

        var summary = await _db.ConversationSummaries
            .SingleOrDefaultAsync(s => s.ConversationId == conversationId);

        if (summary == null)
        {
            summary = new ConversationSummary
            {
                ConversationId = conversationId
            };
            _db.ConversationSummaries.Add(summary);
        }

        summary.MessageCount = messages.Count;
        summary.FirstMessageAtUtc = messages.First().MessageTimestampUtc;
        summary.LastMessageAtUtc = messages.Last().MessageTimestampUtc;
        summary.ParticipantSet = string.Join(
            ",",
            messages.Select(m => m.Role).Distinct().OrderBy(r => r));
        summary.PreviewText = messages.First().Content.Length > 500
            ? messages.First().Content[..500]
            : messages.First().Content;
        summary.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task RebuildAllAsync()
    {
        var conversationIds = await _db.Conversations
            .Select(c => c.ConversationId)
            .ToListAsync();

        foreach (var id in conversationIds)
            await RebuildAsync(id);
    }
}
