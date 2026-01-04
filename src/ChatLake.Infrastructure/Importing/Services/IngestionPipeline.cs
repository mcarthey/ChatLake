using ChatLake.Core.Parsing;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ChatLake.Infrastructure.Importing.Services;

/// <summary>
/// Coordinates raw artifact parsing and canonical persistence.
/// Supports streaming ingestion with batched commits and progress reporting.
/// </summary>
public sealed class IngestionPipeline
{
    private readonly ChatLakeDbContext _db;
    private readonly IRawArtifactParserResolver _parserResolver;
    private readonly IConversationIngestionService _conversationIngestion;
    private readonly IConversationSummaryBuilder _summaryBuilder;

    /// <summary>
    /// Number of conversations to process before committing a batch.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// How often to report progress (every N conversations).
    /// </summary>
    public int ProgressReportInterval { get; set; } = 10;

    public IngestionPipeline(
        ChatLakeDbContext db,
        IRawArtifactParserResolver parserResolver,
        IConversationIngestionService conversationIngestion,
        IConversationSummaryBuilder summaryBuilder)
    {
        _db = db;
        _parserResolver = parserResolver;
        _conversationIngestion = conversationIngestion;
        _summaryBuilder = summaryBuilder;
    }

    /// <summary>
    /// Result of ingestion containing counts and timing.
    /// </summary>
    public record IngestionResult(
        int ConversationCount,
        int FailureCount,
        TimeSpan ElapsedTime);

    /// <summary>
    /// Progress callback delegate.
    /// </summary>
    public delegate Task ProgressCallback(int processedCount, int? totalCount);

    /// <summary>
    /// Ingests a raw artifact with streaming and batched commits.
    /// </summary>
    public async Task<IngestionResult> IngestRawArtifactAsync(
        long rawArtifactId,
        ProgressCallback? onProgress = null,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var artifact = await _db.RawArtifacts
            .AsNoTracking()
            .SingleAsync(a => a.RawArtifactId == rawArtifactId, ct);

        var parser = _parserResolver.Resolve(
            artifact.ArtifactType,
            artifact.ArtifactName);

        var processedCount = 0;
        var failureCount = 0;
        var conversationIds = new List<long>();
        var batchConversationIds = new List<long>();

        try
        {
            await using var stream = await OpenArtifactStreamAsync(artifact);

            await foreach (var convo in parser.ParseAsync(stream, ct))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    // Ingest single conversation
                    await _conversationIngestion.IngestAsync(
                        artifact.ImportBatchId,
                        artifact.RawArtifactId,
                        new[] { convo });

                    // Get the conversation ID for summary building
                    var conversationId = await _db.Conversations
                        .AsNoTracking()
                        .Where(c => c.ExternalConversationId == convo.ExternalConversationId
                            && c.SourceSystem == convo.SourceSystem)
                        .Select(c => c.ConversationId)
                        .SingleAsync(ct);

                    batchConversationIds.Add(conversationId);
                    conversationIds.Add(conversationId);
                    processedCount++;

                    // Report progress periodically
                    if (onProgress != null && processedCount % ProgressReportInterval == 0)
                    {
                        await onProgress(processedCount, null);
                    }

                    // Batch commit - rebuild summaries and save
                    if (batchConversationIds.Count >= BatchSize)
                    {
                        await CommitBatchAsync(batchConversationIds, ct);
                        batchConversationIds.Clear();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Clear any stale/detached entities from the failed ingestion
                    _db.ChangeTracker.Clear();

                    // Log failure but continue processing
                    _db.ParsingFailures.Add(new Conversations.Entities.ParsingFailure
                    {
                        RawArtifactId = artifact.RawArtifactId,
                        FailureStage = "Ingest",
                        FailureMessage = $"Conversation {convo.ExternalConversationId}: {ex.Message}"
                    });
                    await _db.SaveChangesAsync(ct);
                    failureCount++;
                }
            }

            // Commit any remaining conversations
            if (batchConversationIds.Count > 0)
            {
                await CommitBatchAsync(batchConversationIds, ct);
            }

            // Final progress report
            if (onProgress != null)
            {
                await onProgress(processedCount, processedCount);
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _db.ParsingFailures.Add(new Conversations.Entities.ParsingFailure
            {
                RawArtifactId = artifact.RawArtifactId,
                FailureStage = "Parse",
                FailureMessage = ex.Message
            });

            await _db.SaveChangesAsync(ct);
            throw;
        }

        var elapsed = DateTime.UtcNow - startTime;
        return new IngestionResult(processedCount, failureCount, elapsed);
    }

    /// <summary>
    /// Commits a batch of conversations by rebuilding their summaries.
    /// </summary>
    private async Task CommitBatchAsync(List<long> conversationIds, CancellationToken ct)
    {
        foreach (var id in conversationIds)
        {
            ct.ThrowIfCancellationRequested();
            await _summaryBuilder.RebuildAsync(id);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Opens a stream to the artifact content.
    /// Prefers filesystem storage (StoredPath) for large files to avoid memory duplication.
    /// Falls back to RawJson for backwards compatibility.
    /// </summary>
    private static async Task<Stream> OpenArtifactStreamAsync(RawArtifact artifact)
    {
        // Prefer filesystem storage for large artifacts
        if (!string.IsNullOrEmpty(artifact.StoredPath) && File.Exists(artifact.StoredPath))
        {
            return new FileStream(
                artifact.StoredPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);
        }

        // Fall back to RawJson (legacy or small artifacts)
        if (!string.IsNullOrEmpty(artifact.RawJson))
        {
            var bytes = Encoding.UTF8.GetBytes(artifact.RawJson);
            return new MemoryStream(bytes);
        }

        throw new InvalidOperationException(
            $"Artifact {artifact.RawArtifactId} has neither StoredPath nor RawJson content.");
    }
}
