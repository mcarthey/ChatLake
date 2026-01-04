using System.Text.Json;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Persistence;
using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

public sealed class ProjectSuggestionService : IProjectSuggestionService
{
    private readonly ChatLakeDbContext _db;

    public ProjectSuggestionService(ChatLakeDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProjectSuggestionDto>> GetPendingSuggestionsAsync()
    {
        return await GetSuggestionsByStatusAsync("Pending");
    }

    public async Task<IReadOnlyList<ProjectSuggestionDto>> GetSuggestionsByStatusAsync(string status)
    {
        return await _db.ProjectSuggestions
            .Where(ps => ps.Status == status)
            .OrderByDescending(ps => ps.Confidence)
            .Select(ps => ToDto(ps))
            .ToListAsync();
    }

    public async Task<long> AcceptSuggestionAsync(long suggestionId)
    {
        var suggestion = await _db.ProjectSuggestions
            .SingleAsync(ps => ps.ProjectSuggestionId == suggestionId);

        if (suggestion.Status != "Pending")
            throw new InvalidOperationException($"Cannot accept suggestion with status '{suggestion.Status}'");

        // Create the project
        var project = new Project
        {
            ProjectKey = suggestion.SuggestedProjectKey,
            Name = suggestion.SuggestedName,
            Description = suggestion.Summary,
            IsActive = true,
            IsSystemGenerated = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "System"
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Assign conversations to the project
        var conversationIds = JsonSerializer.Deserialize<List<long>>(suggestion.ConversationIdsJson) ?? [];

        foreach (var conversationId in conversationIds)
        {
            _db.ProjectConversations.Add(new ProjectConversation
            {
                ProjectId = project.ProjectId,
                ConversationId = conversationId,
                InferenceRunId = suggestion.InferenceRunId,
                AssignedBy = "System",
                AssignedAtUtc = DateTime.UtcNow,
                Confidence = suggestion.Confidence,
                IsCurrent = true
            });
        }

        // Update suggestion status
        suggestion.Status = "Accepted";
        suggestion.ResolvedProjectId = project.ProjectId;
        suggestion.ResolvedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return project.ProjectId;
    }

    public async Task RejectSuggestionAsync(long suggestionId)
    {
        var suggestion = await _db.ProjectSuggestions
            .SingleAsync(ps => ps.ProjectSuggestionId == suggestionId);

        if (suggestion.Status != "Pending")
            throw new InvalidOperationException($"Cannot reject suggestion with status '{suggestion.Status}'");

        suggestion.Status = "Rejected";
        suggestion.ResolvedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task MergeSuggestionAsync(long suggestionId, long targetProjectId)
    {
        var suggestion = await _db.ProjectSuggestions
            .SingleAsync(ps => ps.ProjectSuggestionId == suggestionId);

        if (suggestion.Status != "Pending")
            throw new InvalidOperationException($"Cannot merge suggestion with status '{suggestion.Status}'");

        var project = await _db.Projects
            .SingleAsync(p => p.ProjectId == targetProjectId);

        // Assign conversations to the existing project
        var conversationIds = JsonSerializer.Deserialize<List<long>>(suggestion.ConversationIdsJson) ?? [];

        foreach (var conversationId in conversationIds)
        {
            // Deactivate any existing current assignments for this conversation
            var existing = await _db.ProjectConversations
                .Where(pc => pc.ProjectId == targetProjectId && pc.ConversationId == conversationId && pc.IsCurrent)
                .ToListAsync();

            foreach (var e in existing)
                e.IsCurrent = false;

            _db.ProjectConversations.Add(new ProjectConversation
            {
                ProjectId = targetProjectId,
                ConversationId = conversationId,
                InferenceRunId = suggestion.InferenceRunId,
                AssignedBy = "System",
                AssignedAtUtc = DateTime.UtcNow,
                Confidence = suggestion.Confidence,
                IsCurrent = true
            });
        }

        // Update suggestion status
        suggestion.Status = "Merged";
        suggestion.ResolvedProjectId = targetProjectId;
        suggestion.ResolvedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ConversationPreviewDto>> GetSuggestionConversationsAsync(long suggestionId)
    {
        var suggestion = await _db.ProjectSuggestions
            .SingleAsync(ps => ps.ProjectSuggestionId == suggestionId);

        var conversationIds = JsonSerializer.Deserialize<List<long>>(suggestion.ConversationIdsJson) ?? [];

        var conversations = await _db.Conversations
            .Where(c => conversationIds.Contains(c.ConversationId))
            .Select(c => new
            {
                c.ConversationId,
                c.FirstMessageAtUtc,
                MessageCount = _db.Messages.Count(m => m.ConversationId == c.ConversationId),
                FirstUserMessage = _db.Messages
                    .Where(m => m.ConversationId == c.ConversationId && m.Role == "user")
                    .OrderBy(m => m.SequenceIndex)
                    .Select(m => m.Content)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return conversations.Select(c => new ConversationPreviewDto(
            ConversationId: c.ConversationId,
            Title: TruncateTitle(c.FirstUserMessage),
            FirstMessageAtUtc: c.FirstMessageAtUtc,
            MessageCount: c.MessageCount
        )).ToList();
    }

    private static string? TruncateTitle(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        var firstLine = content.Split('\n').FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(firstLine))
            return null;

        return firstLine.Length > 80 ? firstLine[..80] + "..." : firstLine;
    }

    private static ProjectSuggestionDto ToDto(ProjectSuggestion ps) => new(
        ProjectSuggestionId: ps.ProjectSuggestionId,
        InferenceRunId: ps.InferenceRunId,
        SuggestedName: ps.SuggestedName,
        SuggestedProjectKey: ps.SuggestedProjectKey,
        Summary: ps.Summary,
        Confidence: ps.Confidence,
        Status: ps.Status,
        ResolvedAtUtc: ps.ResolvedAtUtc,
        ResolvedProjectId: ps.ResolvedProjectId);
}
