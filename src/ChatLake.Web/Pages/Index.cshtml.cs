using ChatLake.Core.Services;
using ChatLake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ChatLakeDbContext _db;
    private readonly IConversationQueryService _conversations;

    public int ConversationCount { get; private set; }
    public int ProjectCount { get; private set; }
    public int TopicCount { get; private set; }
    public int PendingSuggestions { get; private set; }

    public DateTime? LastAnalysisRun { get; private set; }
    public bool HasClustering { get; private set; }
    public bool HasTopics { get; private set; }
    public bool HasSimilarity { get; private set; }

    public IReadOnlyList<ConversationSummaryDto> RecentConversations { get; private set; } = [];

    public IndexModel(ChatLakeDbContext db, IConversationQueryService conversations)
    {
        _db = db;
        _conversations = conversations;
    }

    public async Task OnGetAsync()
    {
        // Get counts
        ConversationCount = await _db.Conversations.CountAsync();
        ProjectCount = await _db.Projects.CountAsync(p => p.IsActive);
        TopicCount = await _db.Topics.Select(t => t.TopicId).Distinct().CountAsync();
        PendingSuggestions = await _db.ProjectSuggestions.CountAsync(ps => ps.Status == "Pending");

        // Check analysis status
        var runs = await _db.InferenceRuns
            .Where(r => r.Status == "Completed")
            .GroupBy(r => r.RunType)
            .Select(g => new { RunType = g.Key, LastRun = g.Max(r => r.CompletedAtUtc) })
            .ToListAsync();

        HasClustering = runs.Any(r => r.RunType == "Clustering");
        HasTopics = runs.Any(r => r.RunType == "Topics");
        HasSimilarity = runs.Any(r => r.RunType == "Similarity");

        LastAnalysisRun = runs.Max(r => r.LastRun);

        // Get recent conversations
        var allSummaries = await _conversations.GetAllSummariesAsync();
        RecentConversations = allSummaries
            .OrderByDescending(c => c.LastMessageAtUtc)
            .Take(5)
            .ToList();
    }
}
