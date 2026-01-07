using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Blog;

public class SuggestionsModel : PageModel
{
    private readonly IBlogSuggestionService _blogService;

    public IReadOnlyList<BlogTopicSuggestionDto> Suggestions { get; private set; } = [];
    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }

    public SuggestionsModel(IBlogSuggestionService blogService)
    {
        _blogService = blogService;
    }

    public async Task OnGetAsync(string? message = null, string? error = null)
    {
        Message = message;
        ErrorMessage = error;
        Suggestions = await _blogService.GetPendingSuggestionsAsync();
    }

    public async Task<IActionResult> OnPostGenerateBlogsAsync()
    {
        try
        {
            var result = await _blogService.EvaluateAndGenerateAsync();

            var message = result.BlogsGenerated > 0
                ? $"Generated {result.BlogsGenerated} blog posts from {result.ClustersEvaluated} clusters in {result.ElapsedTime.TotalSeconds:F1}s"
                : result.ClustersEvaluated > 0
                    ? $"Evaluated {result.ClustersEvaluated} clusters but none passed the quality threshold"
                    : "No eligible clusters found. Make sure you have accepted some project suggestions first.";

            return RedirectToPage(new { message });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Blog generation failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(long id)
    {
        try
        {
            await _blogService.ApproveAsync(id);
            return RedirectToPage(new { message = "Blog suggestion approved" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Approve failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostDismissAsync(long id)
    {
        try
        {
            await _blogService.DismissAsync(id);
            return RedirectToPage(new { message = "Blog suggestion dismissed" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { error = $"Dismiss failed: {ex.Message}" });
        }
    }
}
