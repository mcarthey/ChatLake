using ChatLake.Core.Parsing;
using ChatLake.Core.Parsing.Stubs;

namespace ChatLake.Tests.Core.Parsing;

public sealed class NullRawArtifactParserTests
    : RawArtifactParserContractTests
{
    protected override IRawArtifactParser CreateParser()
        => new NullRawArtifactParser();

    protected override string ValidJsonSample
        => "{}";
}
