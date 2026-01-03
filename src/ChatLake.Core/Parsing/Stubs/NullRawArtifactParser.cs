using System.Collections.Immutable;

namespace ChatLake.Core.Parsing;

public sealed class NullRawArtifactParser : IRawArtifactParser
{
    public IReadOnlyCollection<ParsedConversation> Parse(string rawJson)
        => Array.Empty<ParsedConversation>();
}
