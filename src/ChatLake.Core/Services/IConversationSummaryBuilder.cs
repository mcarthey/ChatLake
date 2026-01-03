namespace ChatLake.Core.Services;

public interface IConversationSummaryBuilder
{
    Task RebuildAsync(long conversationId);
    Task RebuildAllAsync();
}
