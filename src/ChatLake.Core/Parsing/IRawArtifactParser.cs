using System.Collections.Generic;

namespace ChatLake.Core.Parsing;

public interface IRawArtifactParser
{
    /// <summary>
    /// Parses a raw artifact JSON payload into zero or more conversations.
    /// Must be deterministic and side-effect free.
    /// </summary>
    IReadOnlyCollection<ParsedConversation> Parse(string rawJson);
}
