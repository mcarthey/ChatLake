using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class ImportModel : PageModel
{
    private readonly IImportOrchestrator _orchestrator;

    public ImportModel(IImportOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [BindProperty]
    public IFormFile Upload { get; set; } = null!;

    public async Task<IActionResult> OnPostAsync()
    {
        using var reader = new StreamReader(Upload.OpenReadStream());
        var json = await reader.ReadToEndAsync();

        await _orchestrator.ImportJsonArtifactsAsync(
            "ChatGPT",
            "export",
            User.Identity?.Name,
            "UI Import",
            new[]
            {
                new ImportJsonArtifactRequest(
                    "ConversationsJson",
                    Upload.FileName,
                    json)
            });

        return RedirectToPage("/Conversations/Index");
    }
}
