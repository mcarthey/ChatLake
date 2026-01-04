using ChatLake.Core.Parsing;
using System.Collections.Immutable;
using System.Text;
using Xunit;

namespace ChatLake.Tests.Core.Parsing;

public abstract class RawArtifactParserContractTests
{
    protected abstract IRawArtifactParser CreateParser();

    protected abstract string ValidJsonSample { get; }

    private static async Task<ImmutableArray<ParsedConversation>> ParseAsync(
        IRawArtifactParser parser,
        string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var builder = ImmutableArray.CreateBuilder<ParsedConversation>();

        await foreach (var convo in parser.ParseAsync(stream))
        {
            builder.Add(convo);
        }

        return builder.ToImmutable();
    }

    [Fact]
    public async Task Parse_IsDeterministic()
    {
        var parser = CreateParser();

        var result1 = await ParseAsync(parser, ValidJsonSample);
        var result2 = await ParseAsync(parser, ValidJsonSample);

        Assert.Equal(result1.Length, result2.Length);

        for (int i = 0; i < result1.Length; i++)
        {
            Assert.Equal(result1[i], result2[i]);
        }
    }

    [Fact]
    public async Task Parse_NeverReturnsNull()
    {
        var parser = CreateParser();

        var result = await ParseAsync(parser, ValidJsonSample);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Parse_NeverReturnsNullMessages()
    {
        var parser = CreateParser();

        var conversations = await ParseAsync(parser, ValidJsonSample);

        foreach (var conversation in conversations)
        {
            Assert.False(conversation.Messages.IsDefault);
        }
    }
}
