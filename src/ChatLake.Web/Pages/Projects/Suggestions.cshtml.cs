using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Projects;

public class SuggestionsModel : PageModel
{
    private readonly IProjectSuggestionService _suggestions;
    private readonly IClusteringService _clustering;
    private readonly IProjectService _projects;

    public IReadOnlyList<ProjectSuggestionDto> PendingSuggestions { get; private set; } = [];
    public IReadOnlyList<ProjectDto> ExistingProjects { get; private set; } = [];
    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }

    public SuggestionsModel(
        IProjectSuggestionService suggestions,
        IClusteringService clustering,
        IProjectService projects)
    {
        _suggestions = suggestions;
        _clustering = clustering;
        _projects = projects;
    }

    public async Task OnGetAsync(string? message = null, string? error = null)
    {
        Message = message;
        ErrorMessage = error;
        PendingSuggestions = await _suggestions.GetPendingSuggestionsAsync();
        ExistingProjects = await _projects.ListAsync();
    }

    public async Task<IActionResult> OnPostRunClusteringAsync()
    {
        try
        {
            var result = await _clustering.ClusterConversationsAsync();
            return RedirectToPage(new
            {
                message = $"Clustering complete: {result.SuggestionsCreated} suggestions created from {result.ConversationCount} conversations"
            });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Clustering failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostAcceptAsync(long suggestionId)
    {
        try
        {
            var projectId = await _suggestions.AcceptSuggestionAsync(suggestionId);
            return RedirectToPage(new { message = $"Suggestion accepted. Project created (ID: {projectId})" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Accept failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(long suggestionId)
    {
        try
        {
            await _suggestions.RejectSuggestionAsync(suggestionId);
            return RedirectToPage(new { message = "Suggestion rejected" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Reject failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostMergeAsync(long suggestionId, long targetProjectId)
    {
        try
        {
            await _suggestions.MergeSuggestionAsync(suggestionId, targetProjectId);
            return RedirectToPage(new { message = "Suggestion merged into existing project" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Merge failed: {ex.Message}" });
        }
    }
}
