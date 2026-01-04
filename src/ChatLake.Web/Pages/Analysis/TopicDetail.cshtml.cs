using ChatLake.Core.Services;
using ChatLake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Web.Pages.Analysis;

public class TopicDetailModel : PageModel
{
    private readonly ChatLakeDbContext _db;

    public TopicDto? Topic { get; private set; }
    public IReadOnlyList<TopicConversationDto> Conversations { get; private set; } = [];

    public TopicDetailModel(ChatLakeDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var topic = await _db.Topics.FindAsync(id);
        if (topic == null)
        {
            return Page();
        }

        // Parse keywords
        var keywords = new List<string>();
        if (!string.IsNullOrEmpty(topic.KeywordsJson))
        {
            try
            {
                keywords = System.Text.Json.JsonSerializer.Deserialize<List<string>>(topic.KeywordsJson) ?? [];
            }
            catch { }
        }

        // Get conversation count
        var conversationCount = await _db.ConversationTopics
            .Where(ct => ct.TopicId == id)
            .Select(ct => ct.ConversationId)
            .Distinct()
            .CountAsync();

        Topic = new TopicDto(
            TopicId: topic.TopicId,
            Label: topic.Label,
            Keywords: keywords,
            ConversationCount: conversationCount);

        // Get conversations for this topic
        var conversationTopics = await _db.ConversationTopics
            .Where(ct => ct.TopicId == id)
            .OrderByDescending(ct => ct.Score)
            .Take(50)
            .ToListAsync();

        var conversationIds = conversationTopics.Select(ct => ct.ConversationId).ToList();

        var summaries = await _db.ConversationSummaries
            .Where(cs => conversationIds.Contains(cs.ConversationId))
            .ToDictionaryAsync(cs => cs.ConversationId);

        Conversations = conversationTopics
            .Where(ct => summaries.ContainsKey(ct.ConversationId))
            .Select(ct =>
            {
                var summary = summaries[ct.ConversationId];
                return new TopicConversationDto(
                    ConversationId: ct.ConversationId,
                    PreviewText: summary.PreviewText,
                    Score: ct.Score,
                    FirstMessageAtUtc: summary.FirstMessageAtUtc,
                    MessageCount: summary.MessageCount);
            })
            .ToList();

        return Page();
    }
}

public record TopicConversationDto(
    long ConversationId,
    string? PreviewText,
    decimal Score,
    DateTime? FirstMessageAtUtc,
    int MessageCount);
