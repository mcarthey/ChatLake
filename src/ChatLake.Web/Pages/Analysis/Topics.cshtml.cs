using ChatLake.Core.Services;
using ChatLake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Web.Pages.Analysis;

public class TopicsModel : PageModel
{
    private readonly ChatLakeDbContext _db;
    private readonly ITopicExtractionService _topics;

    public IReadOnlyList<TopicDto> Topics { get; private set; } = [];

    public TopicsModel(ChatLakeDbContext db, ITopicExtractionService topics)
    {
        _db = db;
        _topics = topics;
    }

    public async Task OnGetAsync()
    {
        // Get the most recent completed topics run
        var latestRun = await _db.InferenceRuns
            .Where(r => r.RunType == "Topics" && r.Status == "Completed")
            .OrderByDescending(r => r.CompletedAtUtc)
            .FirstOrDefaultAsync();

        if (latestRun != null)
        {
            Topics = await _topics.GetTopicsAsync(latestRun.InferenceRunId);
        }
    }
}
