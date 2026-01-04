using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChatLake.Core.Services;

public interface IImportOrchestrator
{
    Task<long> ImportJsonArtifactsAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel,
        IReadOnlyCollection<ImportJsonArtifactRequest> artifacts,
        CancellationToken ct = default);

    /// <summary>
    /// Imports a single artifact from a stream (memory-efficient for large files).
    /// </summary>
    Task<long> ImportStreamArtifactAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel,
        string artifactType,
        string artifactName,
        Stream content,
        CancellationToken ct = default);
}

public sealed record ImportJsonArtifactRequest(
    string ArtifactType,
    string ArtifactName,
    string JsonPayload);
