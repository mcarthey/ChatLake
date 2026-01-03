namespace ChatLake.Core.Services;

public interface IProjectService
{
    Task<long> CreateAsync(string name, string? description = null);
    Task RenameAsync(long projectId, string newName);
    Task ArchiveAsync(long projectId);

    Task AddConversationAsync(long projectId, long conversationId, string addedBy);
    Task RemoveConversationAsync(long projectId, long conversationId);

    Task<IReadOnlyList<ProjectDto>> ListAsync();
}

public sealed record ProjectDto(
    long ProjectId,
    string Name,
    string Status,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
