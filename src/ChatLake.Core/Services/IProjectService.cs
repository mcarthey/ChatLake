namespace ChatLake.Core.Services;

public interface IProjectService
{
    Task<long> CreateAsync(string name, string? description = null);
    Task RenameAsync(long projectId, string newName);
    Task ArchiveAsync(long projectId);

    Task AddConversationAsync(long projectId, long conversationId, string assignedBy);
    Task RemoveConversationAsync(long projectId, long conversationId);

    Task<IReadOnlyList<ProjectDto>> ListAsync();
    Task<ProjectDetailDto?> GetByIdAsync(long projectId);
    Task<IReadOnlyList<ProjectConversationDto>> GetProjectConversationsAsync(long projectId);
}

public sealed record ProjectDto(
    long ProjectId,
    string ProjectKey,
    string Name,
    bool IsActive,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc,
    int ConversationCount = 0);

public sealed record ProjectDetailDto(
    long ProjectId,
    string ProjectKey,
    string Name,
    string? Description,
    bool IsActive,
    bool IsSystemGenerated,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    int ConversationCount,
    DateTime? FirstConversationAtUtc,
    DateTime? LastConversationAtUtc);

public sealed record ProjectConversationDto(
    long ConversationId,
    string? Title,
    DateTime? FirstMessageAtUtc,
    DateTime? LastMessageAtUtc,
    int MessageCount,
    DateTime AssignedAtUtc,
    string AssignedBy);
