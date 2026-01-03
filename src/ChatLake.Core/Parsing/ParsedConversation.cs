using System.Collections.Immutable;
using System.Linq;

namespace ChatLake.Core.Parsing;

public sealed record ParsedConversation(
    string SourceSystem,
    string? ExternalConversationId,
    ImmutableArray<ParsedMessage> Messages)
{
    public bool Equals(ParsedConversation? other)
    {
        if (other is null) return false;

        return SourceSystem == other.SourceSystem
            && ExternalConversationId == other.ExternalConversationId
            && Messages.SequenceEqual(other.Messages);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = SourceSystem.GetHashCode();
            hash = (hash * 397) ^ (ExternalConversationId?.GetHashCode() ?? 0);

            foreach (var msg in Messages)
                hash = (hash * 397) ^ msg.GetHashCode();

            return hash;
        }
    }
}
