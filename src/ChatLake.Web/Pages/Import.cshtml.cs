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

        // Stream directly to disk - no string allocation for large files
        await using var stream = Upload.OpenReadStream();

        var batchId = await _orchestrator.ImportStreamArtifactAsync(
            sourceSystem: "ChatGPT",
            sourceVersion: null,
            importedBy: User.Identity?.Name,
            importLabel: Upload.FileName,
            artifactType: "ConversationsJson",
            artifactName: Upload.FileName,
            content: stream,
            ct);

        // Redirect to conversations after completion
        return RedirectToPage("/Conversations/Index");
    }
}
