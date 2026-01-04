using ChatLake.Core.Services;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Conversations.Services;

public sealed class ConversationQueryService : IConversationQueryService
{
    private readonly ChatLakeDbContext _db;

    public ConversationQueryService(ChatLakeDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetAllSummariesAsync()
    {
        return await _db.ConversationSummaries
            .OrderByDescending(s => s.LastMessageAtUtc)
            .Select(s => new ConversationSummaryDto(
                s.ConversationId,
                s.MessageCount,
                s.FirstMessageAtUtc,
                s.LastMessageAtUtc,
                s.ParticipantSet,
                s.PreviewText))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetProjectTimelineAsync(long projectId)
    {
        return await _db.ProjectConversations
            .Where(pc => pc.ProjectId == projectId)
            .Join(
                _db.ConversationSummaries,
                pc => pc.ConversationId,
                cs => cs.ConversationId,
                (_, cs) => cs)
            .OrderByDescending(cs => cs.LastMessageAtUtc)
            .Select(cs => new ConversationSummaryDto(
                cs.ConversationId,
                cs.MessageCount,
                cs.FirstMessageAtUtc,
                cs.LastMessageAtUtc,
                cs.ParticipantSet,
                cs.PreviewText))
            .ToListAsync();
    }

    public async Task<ConversationDetailDto> GetConversationAsync(long conversationId)
    {
        var summary = await _db.ConversationSummaries
            .Where(s => s.ConversationId == conversationId)
            .FirstOrDefaultAsync();

        var messages = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SequenceIndex)
            .Select(m => new ConversationMessageDto(
                m.Role,
                m.SequenceIndex,
                m.Content,
                m.MessageTimestampUtc))
            .ToListAsync();

        // Use first 100 chars of preview as title
        var title = summary?.PreviewText?.Length > 100
            ? summary.PreviewText[..100] + "..."
            : summary?.PreviewText;

        return new ConversationDetailDto(
            conversationId,
            title,
            summary?.MessageCount ?? messages.Count,
            summary?.FirstMessageAtUtc,
            summary?.LastMessageAtUtc,
            messages);
    }
}
