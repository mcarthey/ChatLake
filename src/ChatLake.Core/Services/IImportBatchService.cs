using System.Threading.Tasks;

namespace ChatLake.Core.Services;

public interface IImportBatchService
{
    Task<long> CreateAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel);

    /// <summary>
    /// Marks the batch as "Processing" and records the start time.
    /// </summary>
    Task MarkProcessingAsync(long importBatchId);

    /// <summary>
    /// Updates progress during processing.
    /// </summary>
    Task UpdateProgressAsync(long importBatchId, int processedCount, int? totalCount);

    Task MarkCommittedAsync(long importBatchId, int artifactCount, int conversationCount);

    Task MarkFailedAsync(long importBatchId, string? errorMessage);

    /// <summary>
    /// Gets the current status of an import batch.
    /// </summary>
    Task<ImportBatchStatus?> GetStatusAsync(long importBatchId);
}

/// <summary>
/// DTO for import batch status - used for progress polling.
/// </summary>
public record ImportBatchStatus(
    long ImportBatchId,
    string Status,
    int ProcessedConversationCount,
    int? TotalConversationCount,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorMessage)
{
    public TimeSpan? ElapsedTime => StartedAtUtc.HasValue
        ? (CompletedAtUtc ?? DateTime.UtcNow) - StartedAtUtc.Value
        : null;

    public double? ProgressPercentage => TotalConversationCount > 0
        ? (double)ProcessedConversationCount / TotalConversationCount.Value * 100
        : null;

    public bool IsComplete => Status == "Committed" || Status == "Failed";
}
