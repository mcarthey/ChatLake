using System;

namespace ChatLake.Infrastructure.Importing.Entities;

public class RawArtifact
{
    public long RawArtifactId { get; set; }

    public long ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;

    public string ArtifactType { get; set; } = null!;
    public string ArtifactName { get; set; } = null!;
    public string? ContentType { get; set; }

    public long ByteLength { get; set; }

    /// <summary>
    /// SHA-256 hash of raw artifact content
    /// </summary>
    public byte[] Sha256 { get; set; } = null!;

    /// <summary>
    /// Raw JSON payload (when stored in DB)
    /// </summary>
    public string? RawJson { get; set; }

    /// <summary>
    /// Optional filesystem storage path
    /// </summary>
    public string? StoredPath { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
