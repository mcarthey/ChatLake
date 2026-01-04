using ChatLake.Core.Services;
using ChatLake.Infrastructure.Conversations.Entities;
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
        summary.PreviewText = GetPreviewText(messages);
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

    private static string GetPreviewText(List<Message> messages)
    {
        // Find first non-system message that isn't JSON
        var candidate = messages
            .Where(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Where(m => !m.Content.TrimStart().StartsWith('{'))
            .FirstOrDefault();

        if (candidate == null)
        {
            // Fallback: use any non-empty message
            candidate = messages.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Content));
            if (candidate == null)
                return "(no content)";
        }

        var text = candidate.Content.Trim();

        // Truncate to 200 chars with ellipsis
        if (text.Length > 200)
            text = text[..197] + "...";

        return text;
    }
}
