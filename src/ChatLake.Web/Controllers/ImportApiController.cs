using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatLake.Web.Controllers;

[ApiController]
[Route("api/import")]
public class ImportApiController : ControllerBase
{
    private readonly IImportBatchService _importBatchService;
    private readonly IImportCleanupService _cleanupService;

    public ImportApiController(
        IImportBatchService importBatchService,
        IImportCleanupService cleanupService)
    {
        _importBatchService = importBatchService;
        _cleanupService = cleanupService;
    }

    [HttpGet("{batchId:long}/status")]
    public async Task<IActionResult> GetStatus(long batchId)
    {
        var status = await _importBatchService.GetStatusAsync(batchId);

        if (status == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            batchId = status.ImportBatchId,
            status = status.Status,
            processedConversationCount = status.ProcessedConversationCount,
            totalConversationCount = status.TotalConversationCount,
            elapsedSeconds = status.ElapsedTime?.TotalSeconds,
            progressPercentage = status.ProgressPercentage,
            errorMessage = status.ErrorMessage,
            isComplete = status.IsComplete
        });
    }

    [HttpDelete("{batchId:long}")]
    public async Task<IActionResult> CleanupBatch(long batchId, CancellationToken ct)
    {
        var result = await _cleanupService.CleanupBatchAsync(batchId, ct);

        if (result.ErrorMessage != null)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            batchesDeleted = result.BatchesDeleted,
            artifactsDeleted = result.ArtifactsDeleted,
            conversationsDeleted = result.ConversationsDeleted,
            filesDeleted = result.FilesDeleted
        });
    }

    [HttpPost("cleanup-failed")]
    public async Task<IActionResult> CleanupAllFailed(CancellationToken ct)
    {
        var result = await _cleanupService.CleanupAllFailedAsync(ct);

        return Ok(new
        {
            batchesDeleted = result.BatchesDeleted,
            artifactsDeleted = result.ArtifactsDeleted,
            conversationsDeleted = result.ConversationsDeleted,
            filesDeleted = result.FilesDeleted
        });
    }
}
