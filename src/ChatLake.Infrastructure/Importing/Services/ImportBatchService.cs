using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Importing.Services;

public class ImportBatchService : IImportBatchService
{
    private readonly ChatLakeDbContext _dbContext;

    public ImportBatchService(ChatLakeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<long> CreateAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel)
    {
        if (string.IsNullOrWhiteSpace(sourceSystem))
            throw new ArgumentException("Source system is required.", nameof(sourceSystem));

        var batch = new ImportBatch
        {
            SourceSystem = sourceSystem,
            SourceVersion = sourceVersion,
            ImportedBy = importedBy,
            ImportLabel = importLabel,
            Status = "Staged",
            ArtifactCount = 0,
            ImportedAtUtc = DateTime.UtcNow
        };

        _dbContext.ImportBatches.Add(batch);
        await _dbContext.SaveChangesAsync();

        return batch.ImportBatchId;
    }

    public async Task MarkProcessingAsync(long importBatchId)
    {
        var batch = await LoadBatchAsync(importBatchId);

        batch.Status = "Processing";
        batch.StartedAtUtc = DateTime.UtcNow;
        batch.LastHeartbeatUtc = DateTime.UtcNow;
        batch.ProcessedConversationCount = 0;

        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateProgressAsync(long importBatchId, int processedCount, int? totalCount)
    {
        var batch = await LoadBatchAsync(importBatchId);

        batch.ProcessedConversationCount = processedCount;
        batch.TotalConversationCount = totalCount;
        batch.LastHeartbeatUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task MarkCommittedAsync(long importBatchId, int artifactCount, int conversationCount)
    {
        if (artifactCount < 0)
            throw new ArgumentOutOfRangeException(nameof(artifactCount));

        var batch = await LoadBatchAsync(importBatchId);

        batch.Status = "Committed";
        batch.ArtifactCount = artifactCount;
        batch.ProcessedConversationCount = conversationCount;
        batch.CompletedAtUtc = DateTime.UtcNow;
        batch.LastHeartbeatUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task MarkFailedAsync(long importBatchId, string? errorMessage)
    {
        var batch = await LoadBatchAsync(importBatchId);

        batch.Status = "Failed";
        batch.ErrorMessage = errorMessage;
        batch.CompletedAtUtc = DateTime.UtcNow;
        batch.LastHeartbeatUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task<ImportBatchStatus?> GetStatusAsync(long importBatchId)
    {
        var batch = await _dbContext.ImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.ImportBatchId == importBatchId);

        if (batch == null)
            return null;

        return new ImportBatchStatus(
            batch.ImportBatchId,
            batch.Status,
            batch.ProcessedConversationCount,
            batch.TotalConversationCount,
            batch.StartedAtUtc,
            batch.CompletedAtUtc,
            batch.ErrorMessage);
    }

    private async Task<ImportBatch> LoadBatchAsync(long importBatchId)
    {
        var batch = await _dbContext.ImportBatches
            .FirstOrDefaultAsync(b => b.ImportBatchId == importBatchId);

        if (batch == null)
            throw new InvalidOperationException(
                $"ImportBatch with ID {importBatchId} was not found.");

        return batch;
    }
}
