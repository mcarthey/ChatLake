using ChatLake.Core.Services;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Web.Pages.Analysis;

public class IndexModel : PageModel
{
    private readonly ChatLakeDbContext _db;
    private readonly IClusteringService _clustering;
    private readonly ITopicExtractionService _topics;
    private readonly ISimilarityService _similarity;

    public InferenceRun? ClusteringRun { get; private set; }
    public InferenceRun? TopicsRun { get; private set; }
    public InferenceRun? SimilarityRun { get; private set; }
    public IReadOnlyList<InferenceRun> RecentRuns { get; private set; } = [];

    [TempData]
    public string? Message { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public IndexModel(
        ChatLakeDbContext db,
        IClusteringService clustering,
        ITopicExtractionService topics,
        ISimilarityService similarity)
    {
        _db = db;
        _clustering = clustering;
        _topics = topics;
        _similarity = similarity;
    }

    public async Task OnGetAsync()
    {
        // Get the most recent completed run for each type
        ClusteringRun = await _db.InferenceRuns
            .Where(r => r.RunType == "Clustering" && r.Status == "Completed")
            .OrderByDescending(r => r.CompletedAtUtc)
            .FirstOrDefaultAsync();

        TopicsRun = await _db.InferenceRuns
            .Where(r => r.RunType == "Topics" && r.Status == "Completed")
            .OrderByDescending(r => r.CompletedAtUtc)
            .FirstOrDefaultAsync();

        SimilarityRun = await _db.InferenceRuns
            .Where(r => r.RunType == "Similarity" && r.Status == "Completed")
            .OrderByDescending(r => r.CompletedAtUtc)
            .FirstOrDefaultAsync();

        // Get recent runs
        RecentRuns = await _db.InferenceRuns
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(10)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostRunClusteringAsync()
    {
        try
        {
            var result = await _clustering.ClusterConversationsAsync();
            Message = $"Clustering completed: {result.SuggestionsCreated} suggestions created from {result.ConversationCount} conversations.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Clustering failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRunTopicsAsync()
    {
        try
        {
            var result = await _topics.ExtractTopicsAsync();
            Message = $"Topic extraction completed: {result.TopicCount} topics extracted, {result.AssignmentsCreated} assignments created.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Topic extraction failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRunSimilarityAsync()
    {
        try
        {
            var result = await _similarity.CalculateSimilarityAsync();
            Message = $"Similarity calculation completed: {result.PairsStored} pairs stored from {result.ConversationsProcessed} conversations.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Similarity calculation failed: {ex.Message}";
        }

        return RedirectToPage();
    }
}
