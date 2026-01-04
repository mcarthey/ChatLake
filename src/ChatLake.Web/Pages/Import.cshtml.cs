using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Web.Pages;

public class ImportModel : PageModel
{
    private readonly IImportOrchestrator _orchestrator;
    private readonly ChatLakeDbContext _db;

    public ImportModel(IImportOrchestrator orchestrator, ChatLakeDbContext db)
    {
        _orchestrator = orchestrator;
        _db = db;
    }

    [BindProperty]
    public IFormFile? Upload { get; set; }

    /// <summary>
    /// Set when an import is started - triggers progress display.
    /// </summary>
    public long? ImportBatchId { get; set; }

    /// <summary>
    /// Recent imports for display.
    /// </summary>
    public List<ImportBatch> RecentImports { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Load recent imports
        RecentImports = await _db.ImportBatches
            .AsNoTracking()
            .OrderByDescending(b => b.ImportedAtUtc)
            .Take(10)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Upload == null || Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Please select a file to upload.");
            await OnGetAsync();
            return Page();
        }

        string json;
        using (var reader = new StreamReader(Upload.OpenReadStream()))
        {
            json = await reader.ReadToEndAsync(ct);
        }

        // Start the import - this now updates progress in the database
        var batchId = await _orchestrator.ImportJsonArtifactsAsync(
            sourceSystem: "ChatGPT",
            sourceVersion: null,
            importedBy: User.Identity?.Name,
            importLabel: Upload.FileName,
            artifacts: new[]
            {
                new ImportJsonArtifactRequest(
                    ArtifactType: "ConversationsJson",
                    ArtifactName: Upload.FileName,
                    JsonPayload: json)
            },
            ct);

        // Redirect to conversations after completion
        return RedirectToPage("/Conversations/Index");
    }
}
