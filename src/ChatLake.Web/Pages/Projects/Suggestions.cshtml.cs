using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Projects;

public class SuggestionsModel : PageModel
{
    private readonly IProjectSuggestionService _suggestions;
    private readonly IClusteringService _clustering;
    private readonly IProjectService _projects;
    private readonly ISegmentationService _segmentation;

    public IReadOnlyList<ProjectSuggestionDto> PendingSuggestions { get; private set; } = [];
    public IReadOnlyList<ProjectDto> ExistingProjects { get; private set; } = [];
    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }

    public SuggestionsModel(
        IProjectSuggestionService suggestions,
        IClusteringService clustering,
        IProjectService projects,
        ISegmentationService segmentation)
    {
        _suggestions = suggestions;
        _clustering = clustering;
        _projects = projects;
        _segmentation = segmentation;
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
            var message = result.ClusterCount > 0
                ? $"UMAP+HDBSCAN found {result.ClusterCount} natural clusters from {result.SegmentCount} segments. {result.NoiseCount} didn't fit any cluster (noise)."
                : result.SegmentCount > 0
                    ? $"No natural clusters found in {result.SegmentCount} segments - all marked as noise. Try lowering MinClusterSize."
                    : "No segments to cluster.";
            return RedirectToPage(new { message });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Clustering failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostResetAndClusterAsync()
    {
        try
        {
            // Clear existing suggestions
            await _suggestions.ClearAllPendingAsync();

            // Reset all segments and embeddings
            var segmentsDeleted = await _segmentation.ResetAllSegmentsAsync();

            // Run clustering (which will re-segment and re-embed)
            var result = await _clustering.ClusterConversationsAsync();

            var message = $"Reset complete. Deleted {segmentsDeleted} old segments. " +
                (result.ClusterCount > 0
                    ? $"UMAP+HDBSCAN found {result.ClusterCount} clusters from {result.SegmentCount} segments. {result.NoiseCount} noise."
                    : "No clusters found.");

            return RedirectToPage(new { message });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Reset & cluster failed: {ex.Message}" });
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

    public async Task<IActionResult> OnPostClearAllAsync()
    {
        try
        {
            var count = await _suggestions.ClearAllPendingAsync();
            return RedirectToPage(new { message = $"Cleared {count} pending suggestions" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Clear failed: {ex.Message}" });
        }
    }
}
