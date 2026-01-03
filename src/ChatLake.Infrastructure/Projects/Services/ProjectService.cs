using ChatLake.Core.Services;
using ChatLake.Infrastructure.Persistence;
using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Projects.Services;

public sealed class ProjectService : IProjectService
{
    private readonly ChatLakeDbContext _db;

    public ProjectService(ChatLakeDbContext db)
    {
        _db = db;
    }

    public async Task<long> CreateAsync(string name, string? description = null)
    {
        var project = new Project
        {
            Name = name,
            Description = description,
            Status = "Active",
            IsSystemGenerated = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project.ProjectId;
    }

    public async Task RenameAsync(long projectId, string newName)
    {
        var project = await _db.Projects.SingleAsync(p => p.ProjectId == projectId);
        project.Name = newName;
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task ArchiveAsync(long projectId)
    {
        var project = await _db.Projects.SingleAsync(p => p.ProjectId == projectId);
        project.Status = "Archived";
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task AddConversationAsync(long projectId, long conversationId, string addedBy)
    {
        _db.ProjectConversations.Add(new ProjectConversation
        {
            ProjectId = projectId,
            ConversationId = conversationId,
            AddedBy = addedBy,
            AddedAtUtc = DateTime.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // idempotent (already linked)
        }
    }

    public async Task RemoveConversationAsync(long projectId, long conversationId)
    {
        var link = await _db.ProjectConversations
            .SingleAsync(pc => pc.ProjectId == projectId && pc.ConversationId == conversationId);

        _db.ProjectConversations.Remove(link);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync()
    {
        return await _db.Projects
            .OrderBy(p => p.Name)
            .Select(p => new ProjectDto(
                p.ProjectId,
                p.Name,
                p.Status,
                p.IsSystemGenerated,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .ToListAsync();
    }
}
