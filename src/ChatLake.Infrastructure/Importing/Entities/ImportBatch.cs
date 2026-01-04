using System;

namespace ChatLake.Infrastructure.Importing.Entities;

public class ImportBatch
{
    public long ImportBatchId { get; set; }

    public string SourceSystem { get; set; } = null!;
    public string? SourceVersion { get; set; }

    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ImportedBy { get; set; }
    public string? ImportLabel { get; set; }

    public int ArtifactCount { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Staged | Processing | Committed | Failed
    /// </summary>
    public string Status { get; set; } = "Staged";

    // Progress tracking fields
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }

    public int ProcessedConversationCount { get; set; }
    public int? TotalConversationCount { get; set; }

    public string? ErrorMessage { get; set; }

    // Computed properties for UI
    public TimeSpan? ElapsedTime => StartedAtUtc.HasValue
        ? (CompletedAtUtc ?? DateTime.UtcNow) - StartedAtUtc.Value
        : null;

    public double? ProgressPercentage => TotalConversationCount > 0
        ? (double)ProcessedConversationCount / TotalConversationCount.Value * 100
        : null;

    public bool IsComplete => Status == "Committed" || Status == "Failed";
}
