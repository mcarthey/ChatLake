using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChatLake.Core.Services;

public interface IRawArtifactService
{
    Task<long> AddJsonArtifactAsync(
        long importBatchId,
        string artifactType,
        string artifactName,
        string jsonPayload);

    /// <summary>
    /// Adds an artifact by streaming directly to disk.
    /// More memory-efficient for large files.
    /// </summary>
    Task<long> AddStreamArtifactAsync(
        long importBatchId,
        string artifactType,
        string artifactName,
        Stream content,
        CancellationToken ct = default);
}
