namespace ChatLake.Core.Services;

public interface IProjectService
{
    Task<long> CreateAsync(string name, string? description = null);
    Task RenameAsync(long projectId, string newName);
    Task ArchiveAsync(long projectId);

    Task AddConversationAsync(long projectId, long conversationId, string assignedBy);
    Task RemoveConversationAsync(long projectId, long conversationId);

    Task<IReadOnlyList<ProjectDto>> ListAsync();
}

public sealed record ProjectDto(
    long ProjectId,
    string ProjectKey,
    string Name,
    bool IsActive,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc);
