using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using ChatLake.Core.Parsing;

namespace ChatLake.Core.Parsing;

public sealed class NullRawArtifactParser : IRawArtifactParser
{
    public async IAsyncEnumerable<ParsedConversation> ParseAsync(
        Stream jsonStream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Intentionally yields nothing
        await Task.CompletedTask;
        yield break;
    }
}
