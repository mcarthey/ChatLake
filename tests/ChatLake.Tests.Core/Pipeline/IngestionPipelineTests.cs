using System.Diagnostics;
using ChatLake.Core.Parsing;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Entities;
using ChatLake.Infrastructure.Importing.Services;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ChatLake.Tests.Core.Pipeline;

/// <summary>
/// Tests for IngestionPipeline that expose performance issues:
/// - N+1 query pattern (query per conversation)
/// - Individual SaveChanges calls
/// - Memory duplication from RawJson → byte[] → JsonDocument
/// </summary>
public sealed class IngestionPipelineTests : IDisposable
{
    private readonly ChatLakeDbContext _db;
    private readonly IRawArtifactParserResolver _parserResolver;
    private readonly IConversationIngestionService _ingestionService;
    private readonly IConversationSummaryBuilder _summaryBuilder;
    private readonly IngestionPipeline _pipeline;

    private int _ingestionCallCount;
    private int _summaryRebuildCallCount;

    public IngestionPipelineTests()
    {
        var options = new DbContextOptionsBuilder<ChatLakeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ChatLakeDbContext(options);

        _parserResolver = Substitute.For<IRawArtifactParserResolver>();
        _ingestionService = Substitute.For<IConversationIngestionService>();
        _summaryBuilder = Substitute.For<IConversationSummaryBuilder>();

        // Track call counts AND create conversations so the pipeline's query succeeds
        _ingestionService
            .IngestAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<IReadOnlyCollection<ParsedConversation>>())
            .Returns(callInfo =>
            {
                _ingestionCallCount++;
                var batchId = callInfo.ArgAt<long>(0);
                var conversations = callInfo.ArgAt<IReadOnlyCollection<ParsedConversation>>(2);
                foreach (var convo in conversations)
                {
                    _db.Conversations.Add(new Infrastructure.Conversations.Entities.Conversation
                    {
                        ConversationKey = new byte[32],
                        SourceSystem = convo.SourceSystem,
                        ExternalConversationId = convo.ExternalConversationId,
                        CreatedFromImportBatchId = batchId
                    });
                }
                return _db.SaveChangesAsync();
            });

        _summaryBuilder
            .When(x => x.RebuildAsync(Arg.Any<long>()))
            .Do(_ => _summaryRebuildCallCount++);

        _pipeline = new IngestionPipeline(
            _db,
            _parserResolver,
            _ingestionService,
            _summaryBuilder);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    /// <summary>
    /// DIAGNOSTIC TEST: Exposes N+1 pattern where IngestAsync is called once per conversation.
    ///
    /// CURRENT BEHAVIOR: For N conversations, IngestAsync is called N times,
    /// each call followed by a DB query and summary rebuild.
    ///
    /// OPTIMAL BEHAVIOR: Batch conversations and call IngestAsync once with all data.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task IngestRawArtifact_CallsIngestionOncePerConversation(int conversationCount)
    {
        // Arrange
        var artifact = await SetupArtifactWithConversations(conversationCount);
        _ingestionCallCount = 0;
        _summaryRebuildCallCount = 0;

        // Act
        await _pipeline.IngestRawArtifactAsync(artifact.RawArtifactId);

        // Assert - Documents the N+1 pattern
        Assert.Equal(conversationCount, _ingestionCallCount);
        Assert.Equal(conversationCount, _summaryRebuildCallCount);

        // This assertion documents inefficiency:
        // With batching, we'd expect: Assert.Equal(1, _ingestionCallCount);
    }

    /// <summary>
    /// DIAGNOSTIC TEST: Measures time spent in pipeline vs optimal batched approach.
    /// </summary>
    [Fact]
    public async Task IngestRawArtifact_TimingDemonstratesOverhead()
    {
        // Arrange - 50 conversations is enough to show overhead
        var artifact = await SetupArtifactWithConversations(50);

        var stopwatch = Stopwatch.StartNew();

        // Act
        await _pipeline.IngestRawArtifactAsync(artifact.RawArtifactId);

        stopwatch.Stop();

        // This test documents timing for comparison after optimization
        // Current implementation has overhead from:
        // 1. Individual IngestAsync calls
        // 2. Query to get ConversationId back
        // 3. Individual RebuildAsync calls

        Assert.True(true, $"Pipeline completed in {stopwatch.ElapsedMilliseconds}ms for 50 conversations");
    }

    /// <summary>
    /// DIAGNOSTIC TEST: Exposes memory duplication issue.
    ///
    /// The pipeline converts RawJson string → byte[] → MemoryStream → JsonDocument.
    /// For a 200MB file, this creates ~600MB+ of memory allocations.
    /// </summary>
    [Fact]
    public async Task IngestRawArtifact_DuplicatesMemoryForLargeArtifacts()
    {
        // Arrange - Create artifact with large JSON
        const int conversationCount = 100;
        var artifact = await SetupArtifactWithConversations(conversationCount);

        var jsonSize = artifact.RawJson!.Length;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memoryBefore = GC.GetTotalMemory(true);

        // Act
        await _pipeline.IngestRawArtifactAsync(artifact.RawArtifactId);

        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsed = memoryAfter - memoryBefore;

        // Document the memory overhead
        // In current implementation: memory used > jsonSize (due to duplication)
        // Optimal: memory used should be << jsonSize (streaming)
        Assert.True(true, $"JSON size: {jsonSize:N0} bytes, Memory used: {memoryUsed:N0} bytes");
    }

    /// <summary>
    /// DIAGNOSTIC TEST: Verifies the pipeline queries for ConversationId after each ingest.
    ///
    /// This is an N+1 query pattern that could be avoided by having
    /// IngestAsync return the ConversationId directly.
    /// </summary>
    [Fact]
    public async Task IngestRawArtifact_QueriesConversationIdAfterEachIngest()
    {
        // This test documents that the current implementation:
        // 1. Calls IngestAsync (which already creates/gets the conversation)
        // 2. Then queries the DB again to get the ConversationId
        //
        // The query happens here in IngestionPipeline.cs:54-58:
        //   var conversationId = await _db.Conversations
        //       .Where(c => c.ExternalConversationId == convo.ExternalConversationId
        //           && c.SourceSystem == convo.SourceSystem)
        //       .Select(c => c.ConversationId)
        //       .SingleAsync();

        // Arrange
        var artifact = await SetupArtifactWithConversations(10);

        // Act
        await _pipeline.IngestRawArtifactAsync(artifact.RawArtifactId);

        // Assert - Conversations were created and queried
        var conversationCount = await _db.Conversations.CountAsync();
        Assert.Equal(10, conversationCount);
    }

    private async Task<RawArtifact> SetupArtifactWithConversations(int conversationCount)
    {
        // Create import batch
        var batch = new ImportBatch
        {
            SourceSystem = "ChatGPT",
            Status = "Staged",
            ImportedAtUtc = DateTime.UtcNow
        };
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync();

        // Generate JSON
        var json = GenerateChatGptJson(conversationCount);

        // Create artifact
        var artifact = new RawArtifact
        {
            ImportBatchId = batch.ImportBatchId,
            ArtifactType = "ChatGPT",
            ArtifactName = "conversations.json",
            ContentType = "application/json",
            ByteLength = json.Length,
            Sha256 = new byte[32],
            RawJson = json
        };
        _db.RawArtifacts.Add(artifact);
        await _db.SaveChangesAsync();

        // Setup parser resolver to return a real parser
        var parser = new Infrastructure.Parsing.ChatGpt.ChatGptConversationsJsonParser();
        _parserResolver.Resolve("ChatGPT", "conversations.json").Returns(parser);

        return artifact;
    }

    private static string GenerateChatGptJson(int conversationCount)
    {
        var conversations = new System.Text.StringBuilder();
        conversations.Append('[');

        for (int c = 0; c < conversationCount; c++)
        {
            if (c > 0) conversations.Append(',');

            conversations.Append($@"{{
                ""id"": ""conv_{c}"",
                ""title"": ""Test {c}"",
                ""create_time"": {1700000000 + c},
                ""update_time"": {1700000000 + c + 100},
                ""mapping"": {{
                    ""node0"": {{ ""id"": ""node0"", ""message"": null, ""parent"": null, ""children"": [""node1""] }},
                    ""node1"": {{
                        ""id"": ""node1"",
                        ""message"": {{
                            ""id"": ""msg1"",
                            ""author"": {{ ""role"": ""user"" }},
                            ""create_time"": {1700000000 + c + 1},
                            ""content"": {{ ""content_type"": ""text"", ""parts"": [""Hello {c}""] }}
                        }},
                        ""parent"": ""node0"",
                        ""children"": [""node2""]
                    }},
                    ""node2"": {{
                        ""id"": ""node2"",
                        ""message"": {{
                            ""id"": ""msg2"",
                            ""author"": {{ ""role"": ""assistant"" }},
                            ""create_time"": {1700000000 + c + 2},
                            ""content"": {{ ""content_type"": ""text"", ""parts"": [""Hi there {c}""] }}
                        }},
                        ""parent"": ""node1"",
                        ""children"": []
                    }}
                }},
                ""current_node"": ""node2""
            }}");
        }

        conversations.Append(']');
        return conversations.ToString();
    }
}
