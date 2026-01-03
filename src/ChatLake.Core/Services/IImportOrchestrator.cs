using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChatLake.Core.Services;

public interface IImportOrchestrator
{
    Task<long> ImportJsonArtifactsAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel,
        IReadOnlyCollection<ImportJsonArtifactRequest> artifacts);
}

public sealed record ImportJsonArtifactRequest(
    string ArtifactType,
    string ArtifactName,
    string JsonPayload);
