using ChatLake.Core.Parsing;
using ChatLake.Infrastructure.Parsing.ChatGpt;

namespace ChatLake.Infrastructure.Parsing;

public sealed class RawArtifactParserResolver : IRawArtifactParserResolver
{
    public IRawArtifactParser Resolve(string artifactType, string artifactName)
    {
        // ChatGPT export
        if (artifactType == "ConversationsJson"
            || artifactName.Equals("conversations.json", StringComparison.OrdinalIgnoreCase))
        {
            return new ChatGptConversationsJsonParser();
        }

        throw new NotSupportedException(
            $"No parser registered for artifact '{artifactType}' ({artifactName}).");
    }
}
