using ChatLake.Core.Parsing;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
            var parsed = parser.Parse(artifact.RawJson);

            await _conversationIngestion.IngestAsync(
                artifact.ImportBatchId,
                artifact.RawArtifactId,
                parsed);

            foreach (var convo in parsed)
            {
                var conversation = await _db.Conversations
                    .SingleAsync(c => c.ExternalConversationId == convo.ExternalConversationId);

                await _summaryBuilder.RebuildAsync(conversation.ConversationId);
            }

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
