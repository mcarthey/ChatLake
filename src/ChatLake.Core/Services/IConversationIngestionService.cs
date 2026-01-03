using System.Collections.Generic;
using System.Threading.Tasks;
using ChatLake.Core.Parsing;

namespace ChatLake.Core.Services;

/// <summary>
/// Persists parsed conversations and messages into the canonical store.
/// </summary>
public interface IConversationIngestionService
{
    Task IngestAsync(
        long importBatchId,
        long rawArtifactId,
        IReadOnlyCollection<ParsedConversation> parsedConversations);
}
