using ChatLake.Core.Services;

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

    public async Task<long> ImportStreamArtifactAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel,
        string artifactType,
        string artifactName,
        Stream content,
        CancellationToken ct = default)
    {
        var batchId = await _importBatchService.CreateAsync(
            sourceSystem,
            sourceVersion,
            importedBy,
            importLabel);

        try
        {
            await _importBatchService.MarkProcessingAsync(batchId);

            // Stream artifact directly to disk
            var rawArtifactId = await _rawArtifactService.AddStreamArtifactAsync(
                batchId,
                artifactType,
                artifactName,
                content,
                ct);

            // Progress callback
            async Task OnProgress(int processedCount, int? total)
            {
                await _importBatchService.UpdateProgressAsync(batchId, processedCount, total);
            }

            var result = await _ingestionPipeline.IngestRawArtifactAsync(
                rawArtifactId,
                OnProgress,
                ct);

            await _importBatchService.MarkCommittedAsync(batchId, 1, result.ConversationCount);

            return batchId;
        }
        catch (OperationCanceledException)
        {
            await _importBatchService.MarkFailedAsync(batchId, "Import was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            await _importBatchService.MarkFailedAsync(batchId, ex.Message);
            throw;
        }
    }

    public async Task<long> ImportJsonArtifactsAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel,
        IReadOnlyCollection<ImportJsonArtifactRequest> artifacts,
        CancellationToken ct = default)
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
            // Mark as processing
            await _importBatchService.MarkProcessingAsync(batchId);

            var totalConversations = 0;

            foreach (var artifact in artifacts)
            {
                ct.ThrowIfCancellationRequested();

                var rawArtifactId = await _rawArtifactService.AddJsonArtifactAsync(
                    batchId,
                    artifact.ArtifactType,
                    artifact.ArtifactName,
                    artifact.JsonPayload);

                // Progress callback - updates the batch status
                async Task OnProgress(int processedCount, int? total)
                {
                    await _importBatchService.UpdateProgressAsync(
                        batchId,
                        totalConversations + processedCount,
                        total.HasValue ? totalConversations + total.Value : null);
                }

                var result = await _ingestionPipeline.IngestRawArtifactAsync(
                    rawArtifactId,
                    OnProgress,
                    ct);

                totalConversations += result.ConversationCount;
            }

            await _importBatchService.MarkCommittedAsync(
                batchId,
                artifacts.Count,
                totalConversations);

            return batchId;
        }
        catch (OperationCanceledException)
        {
            await _importBatchService.MarkFailedAsync(batchId, "Import was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            await _importBatchService.MarkFailedAsync(batchId, ex.Message);
            throw;
        }
    }
}
