using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChatLake.Web.Controllers;

[ApiController]
[Route("api/import")]
public class ImportApiController : ControllerBase
{
    private readonly IImportBatchService _importBatchService;

    public ImportApiController(IImportBatchService importBatchService)
    {
        _importBatchService = importBatchService;
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
}
