using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Blog;

public class DetailModel : PageModel
{
    private readonly IBlogSuggestionService _blogService;

    public BlogTopicSuggestionDetailDto? Blog { get; private set; }
    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool ShowRaw { get; private set; }

    public DetailModel(IBlogSuggestionService blogService)
    {
        _blogService = blogService;
    }

    public async Task<IActionResult> OnGetAsync(long id, string? message = null, string? error = null, bool raw = false)
    {
        Message = message;
        ErrorMessage = error;
        ShowRaw = raw;

        Blog = await _blogService.GetSuggestionDetailAsync(id);

        if (Blog == null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(long id)
    {
        try
        {
            await _blogService.ApproveAsync(id);
            return RedirectToPage("/Blog/Suggestions", new { message = "Blog approved!" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { id, error = $"Approve failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostDismissAsync(long id)
    {
        try
        {
            await _blogService.DismissAsync(id);
            return RedirectToPage("/Blog/Suggestions", new { message = "Blog dismissed" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { id, error = $"Dismiss failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostRegenerateAsync(long id)
    {
        try
        {
            await _blogService.RegenerateContentAsync(id);
            return RedirectToPage(new { id, message = "Content regenerated!" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { id, error = $"Regenerate failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnPostReEvaluateAsync(long id)
    {
        try
        {
            await _blogService.ReEvaluateAsync(id);
            return RedirectToPage(new { id, message = "Scores re-evaluated!" });
        }
        catch (Exception ex)
        {
            return RedirectToPage(new { id, error = $"Re-evaluate failed: {ex.Message}" });
        }
    }

    public async Task<IActionResult> OnGetExportAsync(long id)
    {
        var markdown = await _blogService.ExportToMarkdownAsync(id);
        var blog = await _blogService.GetSuggestionDetailAsync(id);

        var filename = SanitizeFilename(blog?.Title ?? "blog") + ".md";

        return File(
            System.Text.Encoding.UTF8.GetBytes(markdown),
            "text/markdown",
            filename);
    }

    private static string SanitizeFilename(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(title.Where(c => !invalid.Contains(c)).ToArray());
        return sanitized.Length > 50 ? sanitized[..50] : sanitized;
    }
}
