using ChatLake.Core.Parsing;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ChatLake.Infrastructure.Importing.Services;

/// <summary>
/// Coordinates raw artifact parsing and canonical persistence.
/// </summary>
public sealed class IngestionPipeline
{
    private readonly ChatLakeDbContext _db;
    private readonly IRawArtifactParserResolver _parserResolver;
    private readonly IConversationIngestionService _conversationIngestion;
    private readonly IConversationSummaryBuilder _summaryBuilder;

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

    public async Task IngestRawArtifactAsync(long rawArtifactId)
    {
        var artifact = await _db.RawArtifacts
            .AsNoTracking()
            .SingleAsync(a => a.RawArtifactId == rawArtifactId);

        var parser = _parserResolver.Resolve(
            artifact.ArtifactType,
            artifact.ArtifactName);

        try
        {
            await using var stream = await OpenArtifactStreamAsync(artifact);

            await foreach (var convo in parser.ParseAsync(stream))
            {
                await _conversationIngestion.IngestAsync(
                    artifact.ImportBatchId,
                    artifact.RawArtifactId,
                    new[] { convo });

                var conversationId = await _db.Conversations
                    .AsNoTracking()
                    .Where(c => c.ExternalConversationId == convo.ExternalConversationId
                        && c.SourceSystem == convo.SourceSystem)
                    .Select(c => c.ConversationId)
                    .SingleAsync();

                await _summaryBuilder.RebuildAsync(conversationId);
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _db.ParsingFailures.Add(new Conversations.Entities.ParsingFailure
            {
                RawArtifactId = artifact.RawArtifactId,
                FailureStage = "Parse",
                FailureMessage = ex.Message
            });

            await _db.SaveChangesAsync();
        }
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
            // Convert to bytes - this creates one copy, but the parser will read from the stream
            var bytes = Encoding.UTF8.GetBytes(artifact.RawJson);
            return new MemoryStream(bytes);
        }

        throw new InvalidOperationException(
            $"Artifact {artifact.RawArtifactId} has neither StoredPath nor RawJson content.");
    }
}
