using ChatLake.Core.Parsing;

namespace ChatLake.Tests.Core.Parsing;

public sealed class NullRawArtifactParserTests
    : RawArtifactParserContractTests
{
    protected override IRawArtifactParser CreateParser()
        => new NullRawArtifactParser();

    protected override string ValidJsonSample
        => "{}";
}
