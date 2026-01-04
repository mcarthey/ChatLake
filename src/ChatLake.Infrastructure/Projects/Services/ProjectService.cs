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
        var projectKey = GenerateProjectKey(name);

        var project = new Project
        {
            ProjectKey = projectKey,
            Name = name,
            Description = description,
            IsActive = true,
            IsSystemGenerated = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project.ProjectId;
    }

    public async Task RenameAsync(long projectId, string newName)
    {
        var project = await _db.Projects.SingleAsync(p => p.ProjectId == projectId);
        project.Name = newName;
        await _db.SaveChangesAsync();
    }

    public async Task ArchiveAsync(long projectId)
    {
        var project = await _db.Projects.SingleAsync(p => p.ProjectId == projectId);
        project.IsActive = false;
        await _db.SaveChangesAsync();
    }

    public async Task AddConversationAsync(long projectId, long conversationId, string assignedBy)
    {
        // Deactivate any existing current assignments for this pair
        var existing = await _db.ProjectConversations
            .Where(pc => pc.ProjectId == projectId && pc.ConversationId == conversationId && pc.IsCurrent)
            .ToListAsync();

        foreach (var e in existing)
            e.IsCurrent = false;

        _db.ProjectConversations.Add(new ProjectConversation
        {
            ProjectId = projectId,
            ConversationId = conversationId,
            AssignedBy = assignedBy,
            AssignedAtUtc = DateTime.UtcNow,
            IsCurrent = true
        });

        await _db.SaveChangesAsync();
    }

    public async Task RemoveConversationAsync(long projectId, long conversationId)
    {
        var link = await _db.ProjectConversations
            .Where(pc => pc.ProjectId == projectId && pc.ConversationId == conversationId && pc.IsCurrent)
            .SingleOrDefaultAsync();

        if (link != null)
        {
            link.IsCurrent = false;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync()
    {
        return await _db.Projects
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectDto(
                p.ProjectId,
                p.ProjectKey,
                p.Name,
                p.IsActive,
                p.IsSystemGenerated,
                p.CreatedAtUtc))
            .ToListAsync();
    }

    private static string GenerateProjectKey(string name)
    {
        // Create URL-friendly slug from name
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");

        // Remove non-alphanumeric characters except hyphens
        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        // Add timestamp suffix for uniqueness
        return $"{slug}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }
}
