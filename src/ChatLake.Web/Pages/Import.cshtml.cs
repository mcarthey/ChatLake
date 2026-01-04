using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages;

public class ImportModel : PageModel
{
    private readonly IImportOrchestrator _orchestrator;

    public ImportModel(IImportOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [BindProperty]
    public IFormFile? Upload { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Upload == null || Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Please select a file to upload.");
            return Page();
        }

        string json;
        using (var reader = new StreamReader(Upload.OpenReadStream()))
        {
            json = await reader.ReadToEndAsync();
        }

        await _orchestrator.ImportJsonArtifactsAsync(
            sourceSystem: "ChatGPT",
            sourceVersion: null,
            importedBy: User.Identity?.Name,
            importLabel: "Web import",
            artifacts: new[]
            {
            new ImportJsonArtifactRequest(
                ArtifactType: "ConversationsJson",
                ArtifactName: Upload.FileName,
                JsonPayload: json)
            });

        return RedirectToPage("/Conversations/Index");
    }

}
