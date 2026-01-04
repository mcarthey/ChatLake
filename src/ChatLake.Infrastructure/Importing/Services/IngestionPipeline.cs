using ChatLake.Core.Parsing;
using ChatLake.Core.Services;
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
            await using var stream = new MemoryStream(
                Encoding.UTF8.GetBytes(artifact.RawJson));

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

}
