using ChatLake.Core.Services;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Importing.Services;

public class ImportCleanupService : IImportCleanupService
{
    private readonly ChatLakeDbContext _db;
    private readonly TimeSpan _staleThreshold = TimeSpan.FromHours(1);

    public ImportCleanupService(ChatLakeDbContext db)
    {
        _db = db;
    }

    public async Task<CleanupResult> CleanupBatchAsync(long importBatchId, CancellationToken ct = default)
    {
        var batch = await _db.ImportBatches
            .FirstOrDefaultAsync(b => b.ImportBatchId == importBatchId, ct);

        if (batch == null)
        {
            return new CleanupResult(0, 0, 0, 0, $"Batch {importBatchId} not found.");
        }

        // Only allow cleanup of non-committed batches
        if (batch.Status == "Committed")
        {
            return new CleanupResult(0, 0, 0, 0,
                "Cannot cleanup committed batches. Use a different mechanism to remove imported data.");
        }

        // For Processing batches, check if stale (no heartbeat for > threshold, or no heartbeat at all)
        if (batch.Status == "Processing")
        {
            var isStale = !batch.LastHeartbeatUtc.HasValue
                || DateTime.UtcNow - batch.LastHeartbeatUtc.Value > _staleThreshold;

            if (!isStale)
            {
                return new CleanupResult(0, 0, 0, 0,
                    $"Batch is still processing (last heartbeat {(DateTime.UtcNow - batch.LastHeartbeatUtc!.Value).TotalMinutes:F0} min ago). " +
                    "Wait for completion or wait 1 hour for stale detection.");
            }
        }

        return await DeleteBatchAsync(batch.ImportBatchId, ct);
    }

    public async Task<CleanupResult> CleanupAllFailedAsync(CancellationToken ct = default)
    {
        var staleThreshold = DateTime.UtcNow - _staleThreshold;

        var batchIds = await _db.ImportBatches
            .Where(b => b.Status == "Failed"
                || (b.Status == "Processing" && (b.LastHeartbeatUtc == null || b.LastHeartbeatUtc < staleThreshold))
                || (b.Status == "Staged" && b.ImportedAtUtc < staleThreshold))
            .Select(b => b.ImportBatchId)
            .ToListAsync(ct);

        var totalResult = new CleanupResult(0, 0, 0, 0);

        foreach (var batchId in batchIds)
        {
            ct.ThrowIfCancellationRequested();
            var result = await DeleteBatchAsync(batchId, ct);
            totalResult = new CleanupResult(
                totalResult.BatchesDeleted + result.BatchesDeleted,
                totalResult.ArtifactsDeleted + result.ArtifactsDeleted,
                totalResult.ConversationsDeleted + result.ConversationsDeleted,
                totalResult.FilesDeleted + result.FilesDeleted);
        }

        return totalResult;
    }

    private async Task<CleanupResult> DeleteBatchAsync(long batchId, CancellationToken ct)
    {
        var artifactsDeleted = 0;
        var conversationsDeleted = 0;
        var filesDeleted = 0;

        // Get artifact IDs and file paths for this batch
        var artifacts = await _db.RawArtifacts
            .Where(a => a.ImportBatchId == batchId)
            .Select(a => new { a.RawArtifactId, a.StoredPath })
            .ToListAsync(ct);

        var artifactIds = artifacts.Select(a => a.RawArtifactId).ToList();

        // 1. Delete conversations created from this batch
        // (Cascades to: Messages, ConversationSummaries, ConversationArtifactMaps)
        var conversations = await _db.Conversations
            .Where(c => c.CreatedFromImportBatchId == batchId)
            .ToListAsync(ct);

        conversationsDeleted = conversations.Count;
        _db.Conversations.RemoveRange(conversations);
        await _db.SaveChangesAsync(ct);

        // 2. Delete parsing failures for artifacts in this batch
        var failures = await _db.ParsingFailures
            .Where(f => artifactIds.Contains(f.RawArtifactId))
            .ToListAsync(ct);

        _db.ParsingFailures.RemoveRange(failures);

        // 3. Delete raw artifacts
        var rawArtifacts = await _db.RawArtifacts
            .Where(a => a.ImportBatchId == batchId)
            .ToListAsync(ct);

        artifactsDeleted = rawArtifacts.Count;
        _db.RawArtifacts.RemoveRange(rawArtifacts);
        await _db.SaveChangesAsync(ct);

        // 4. Delete files from disk
        foreach (var artifact in artifacts)
        {
            if (!string.IsNullOrEmpty(artifact.StoredPath) && File.Exists(artifact.StoredPath))
            {
                try
                {
                    File.Delete(artifact.StoredPath);
                    filesDeleted++;

                    // Try to delete empty parent directory
                    var dir = Path.GetDirectoryName(artifact.StoredPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)
                        && !Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                    }
                }
                catch
                {
                    // Ignore file deletion errors - not critical
                }
            }
        }

        // 5. Delete the import batch
        var batch = await _db.ImportBatches
            .FirstOrDefaultAsync(b => b.ImportBatchId == batchId, ct);

        if (batch != null)
        {
            _db.ImportBatches.Remove(batch);
            await _db.SaveChangesAsync(ct);
        }

        return new CleanupResult(1, artifactsDeleted, conversationsDeleted, filesDeleted);
    }
}
