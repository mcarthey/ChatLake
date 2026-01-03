namespace ChatLake.Core.Services;

public interface IConversationQueryService
{
    Task<IReadOnlyList<ConversationSummaryDto>> GetAllSummariesAsync();
    Task<IReadOnlyList<ConversationSummaryDto>> GetProjectTimelineAsync(long projectId);
    Task<ConversationDetailDto> GetConversationAsync(long conversationId);
}

public sealed record ConversationSummaryDto(
    long ConversationId,
    int MessageCount,
    DateTime? FirstMessageAtUtc,
    DateTime? LastMessageAtUtc,
    string ParticipantSet,
    string PreviewText);

public sealed record ConversationMessageDto(
    string Role,
    int SequenceIndex,
    string Content,
    DateTime? MessageTimestampUtc);

public sealed record ConversationDetailDto(
    long ConversationId,
    IReadOnlyList<ConversationMessageDto> Messages);
