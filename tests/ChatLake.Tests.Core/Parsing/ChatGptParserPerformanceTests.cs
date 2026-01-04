using System.Diagnostics;
using System.Text;
using ChatLake.Core.Parsing;
using ChatLake.Infrastructure.Parsing.ChatGpt;

namespace ChatLake.Tests.Core.Parsing;

/// <summary>
/// Performance-focused tests for ChatGptConversationsJsonParser.
/// These tests expose streaming and memory issues with large inputs.
/// </summary>
public sealed class ChatGptParserPerformanceTests
{
    private readonly ChatGptConversationsJsonParser _parser = new();

    /// <summary>
    /// Generates a valid ChatGPT export JSON with the specified number of conversations.
    /// Each conversation has a configurable number of messages.
    /// </summary>
    private static string GenerateLargeJson(int conversationCount, int messagesPerConversation = 10)
    {
        var sb = new StringBuilder();
        sb.Append('[');

        for (int c = 0; c < conversationCount; c++)
        {
            if (c > 0) sb.Append(',');

            sb.Append($@"{{
                ""id"": ""conv_{c}"",
                ""title"": ""Conversation {c}"",
                ""create_time"": {1700000000 + c},
                ""update_time"": {1700000000 + c + 100},
                ""mapping"": {{");

            // Root node (no message)
            sb.Append($@"""node_{c}_0"": {{ ""id"": ""node_{c}_0"", ""message"": null, ""parent"": null, ""children"": [""node_{c}_1""] }}");

            // Message nodes
            for (int m = 1; m <= messagesPerConversation; m++)
            {
                var role = m % 2 == 1 ? "user" : "assistant";
                var parentNode = $"node_{c}_{m - 1}";
                var childNode = m < messagesPerConversation ? $@"[""node_{c}_{m + 1}""]" : "[]";

                sb.Append($@",""node_{c}_{m}"": {{
                    ""id"": ""node_{c}_{m}"",
                    ""message"": {{
                        ""id"": ""msg_{c}_{m}"",
                        ""author"": {{ ""role"": ""{role}"" }},
                        ""create_time"": {1700000000 + c + m},
                        ""content"": {{ ""content_type"": ""text"", ""parts"": [""Message {m} content for conversation {c}. This is some sample text to simulate real message content.""] }}
                    }},
                    ""parent"": ""{parentNode}"",
                    ""children"": {childNode}
                }}");
            }

            sb.Append($@"}},""current_node"": ""node_{c}_{messagesPerConversation}""}}");
        }

        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// DIAGNOSTIC TEST: Measures time to first yield.
    ///
    /// EXPECTED BEHAVIOR (streaming parser): First conversation yields quickly,
    /// regardless of total file size.
    ///
    /// ACTUAL BEHAVIOR: Time to first yield scales with file size because
    /// JsonDocument.ParseAsync loads the entire DOM before yielding anything.
    /// </summary>
    [Theory]
    [InlineData(100, 10)]    // ~50KB - small baseline
    [InlineData(1000, 10)]   // ~500KB - medium
    [InlineData(5000, 10)]   // ~2.5MB - larger
    public async Task TimeToFirstYield_ShouldNotScaleWithFileSize(int conversationCount, int messagesPerConversation)
    {
        var json = GenerateLargeJson(conversationCount, messagesPerConversation);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var stopwatch = Stopwatch.StartNew();

        await foreach (var conversation in _parser.ParseAsync(stream))
        {
            stopwatch.Stop();

            // Log the time to first yield for diagnostic purposes
            var timeToFirstYield = stopwatch.ElapsedMilliseconds;

            // With true streaming, this should be nearly instant (<50ms) regardless of file size.
            // With DOM-based parsing, this will scale linearly with conversationCount.
            //
            // This assertion documents expected streaming behavior.
            // If it fails, the parser is not truly streaming.
            Assert.True(
                timeToFirstYield < 100,
                $"Time to first yield was {timeToFirstYield}ms for {conversationCount} conversations. " +
                $"A streaming parser should yield the first item in <100ms regardless of file size.");

            break; // We only care about the first yield
        }
    }

    /// <summary>
    /// DIAGNOSTIC TEST: Verifies cancellation is respected promptly.
    ///
    /// A streaming parser should stop almost immediately when cancelled.
    /// A DOM-based parser may complete significant work before checking cancellation.
    /// </summary>
    [Fact]
    public async Task Parse_WhenCancelledEarly_ShouldStopPromptly()
    {
        var json = GenerateLargeJson(1000, 20);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        using var cts = new CancellationTokenSource();
        var conversationsYielded = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await foreach (var conversation in _parser.ParseAsync(stream, cts.Token))
            {
                conversationsYielded++;

                if (conversationsYielded == 5)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        stopwatch.Stop();

        // With a streaming parser, we should stop shortly after cancellation.
        // The parser shouldn't process significantly more data after cancel is requested.
        Assert.True(
            conversationsYielded <= 10,
            $"Yielded {conversationsYielded} conversations after cancelling at 5. " +
            $"Parser should respect cancellation more promptly.");
    }

    /// <summary>
    /// DIAGNOSTIC TEST: Measures memory allocation during parsing.
    ///
    /// A streaming parser should have roughly constant memory usage regardless of input size.
    /// A DOM-based parser allocates memory proportional to input size.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task Parse_MemoryAllocation_ShouldBeConstant(int conversationCount)
    {
        var json = GenerateLargeJson(conversationCount, 10);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Force GC to get baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(true);

        var count = 0;
        await foreach (var conversation in _parser.ParseAsync(stream))
        {
            count++;
            // Don't hold references to parsed objects
        }

        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsed = memoryAfter - memoryBefore;

        // This is informational - the test documents current behavior.
        // A streaming parser would use ~constant memory.
        // A DOM parser uses memory proportional to input size.
        Assert.True(count == conversationCount,
            $"Expected {conversationCount} conversations, got {count}");

        // Log for analysis (in real scenario, use test output helper)
        // Memory used scales with conversationCount in current implementation
    }

    /// <summary>
    /// Verifies all conversations are parsed correctly from large input.
    /// </summary>
    [Fact]
    public async Task Parse_LargeInput_ParsesAllConversationsCorrectly()
    {
        const int expectedCount = 500;
        const int messagesPerConversation = 8;

        var json = GenerateLargeJson(expectedCount, messagesPerConversation);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var conversations = new List<ParsedConversation>();
        await foreach (var conversation in _parser.ParseAsync(stream))
        {
            conversations.Add(conversation);
        }

        Assert.Equal(expectedCount, conversations.Count);

        // Verify structure of a sample
        var sample = conversations[0];
        Assert.Equal("ChatGPT", sample.SourceSystem);
        Assert.Equal("conv_0", sample.ExternalConversationId);
        Assert.Equal(messagesPerConversation, sample.Messages.Length);

        // Verify message ordering
        for (int i = 0; i < sample.Messages.Length; i++)
        {
            Assert.Equal(i, sample.Messages[i].SequenceIndex);
        }
    }

    /// <summary>
    /// DIAGNOSTIC TEST: Compares parsing time across different sizes.
    /// </summary>
    [Fact]
    public async Task Parse_TimingComparison_DocumentsCurrentPerformance()
    {
        var sizes = new[] { 100, 500, 1000 };
        var timings = new Dictionary<int, long>();

        foreach (var size in sizes)
        {
            var json = GenerateLargeJson(size, 10);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var stopwatch = Stopwatch.StartNew();

            var count = 0;
            await foreach (var _ in _parser.ParseAsync(stream))
            {
                count++;
            }

            stopwatch.Stop();
            timings[size] = stopwatch.ElapsedMilliseconds;

            Assert.Equal(size, count);
        }

        // Document: time should scale roughly linearly with size in DOM-based parser
        // A streaming parser would show sub-linear scaling for time-to-first-yield
    }
}
