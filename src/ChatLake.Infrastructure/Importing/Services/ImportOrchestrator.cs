using ChatLake.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Importing.Services;

public class ImportOrchestrator : IImportOrchestrator
{
    private readonly IImportBatchService _importBatchService;
    private readonly IRawArtifactService _rawArtifactService;
    private readonly IngestionPipeline _ingestionPipeline;

    public ImportOrchestrator(
        IImportBatchService importBatchService,
        IRawArtifactService rawArtifactService,
        IngestionPipeline ingestionPipeline)
    {
        _importBatchService = importBatchService;
        _rawArtifactService = rawArtifactService;
        _ingestionPipeline = ingestionPipeline;
    }

    public async Task<long> ImportJsonArtifactsAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel,
        IReadOnlyCollection<ImportJsonArtifactRequest> artifacts)
    {
        if (artifacts == null || artifacts.Count == 0)
            throw new ArgumentException("At least one artifact is required.", nameof(artifacts));

        var batchId = await _importBatchService.CreateAsync(
            sourceSystem,
            sourceVersion,
            importedBy,
            importLabel);

        try
        {
            foreach (var artifact in artifacts)
            {
                var rawArtifactId = await _rawArtifactService.AddJsonArtifactAsync(
                    batchId,
                    artifact.ArtifactType,
                    artifact.ArtifactName,
                    artifact.JsonPayload);

                await _ingestionPipeline.IngestRawArtifactAsync(rawArtifactId);
            }

            await _importBatchService.MarkCommittedAsync(
                batchId,
                artifacts.Count);

            return batchId;
        }
        catch (Exception ex)
        {
            await _importBatchService.MarkFailedAsync(
                batchId,
                ex.Message);

            throw;
        }
    }
}
