using System.Collections.Generic;

namespace ChatLake.Core.Parsing.Stubs;

public sealed class NullRawArtifactParser : IRawArtifactParser
{
    public IReadOnlyCollection<ParsedConversation> Parse(string rawJson)
        => Array.Empty<ParsedConversation>();
}
