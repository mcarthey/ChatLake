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
    /// Staged | Committed | Failed
    /// </summary>
    public string Status { get; set; } = "Staged";
}
