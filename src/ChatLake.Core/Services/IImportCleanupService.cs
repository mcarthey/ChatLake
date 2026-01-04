using System.Threading;
using System.Threading.Tasks;

namespace ChatLake.Core.Services;

public interface IImportCleanupService
{
    /// <summary>
    /// Deletes a failed or incomplete import batch and all related data.
    /// Only batches with Status = "Failed" or "Processing" (stale) can be cleaned up.
    /// </summary>
    /// <returns>Summary of what was deleted.</returns>
    Task<CleanupResult> CleanupBatchAsync(long importBatchId, CancellationToken ct = default);

    /// <summary>
    /// Cleans up all failed and stale (processing for > 1 hour) import batches.
    /// </summary>
    Task<CleanupResult> CleanupAllFailedAsync(CancellationToken ct = default);
}

public record CleanupResult(
    int BatchesDeleted,
    int ArtifactsDeleted,
    int ConversationsDeleted,
    int FilesDeleted,
    string? ErrorMessage = null);
