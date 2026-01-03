using ChatLake.Core.Parsing;
using Xunit;

namespace ChatLake.Tests.Core.Parsing;

public abstract class RawArtifactParserContractTests
{
    protected abstract IRawArtifactParser CreateParser();

    protected abstract string ValidJsonSample { get; }

    [Fact]
    public void Parse_IsDeterministic()
    {
        var parser = CreateParser();

        var result1 = parser.Parse(ValidJsonSample);
        var result2 = parser.Parse(ValidJsonSample);

        Assert.Equal(result1.Count, result2.Count);

        for (int i = 0; i < result1.Count; i++)
        {
            Assert.Equal(result1.ElementAt(i), result2.ElementAt(i));
        }
    }

    [Fact]
    public void Parse_NeverReturnsNull()
    {
        var parser = CreateParser();

        var result = parser.Parse(ValidJsonSample);

        Assert.NotNull(result);
    }

    [Fact]
    public void Parse_NeverReturnsNullMessages()
    {
        var parser = CreateParser();

        var conversations = parser.Parse(ValidJsonSample);

        foreach (var conversation in conversations)
        {
            Assert.False(conversation.Messages.IsDefault);
        }
    }
}
