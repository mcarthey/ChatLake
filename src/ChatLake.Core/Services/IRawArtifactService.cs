using System.Threading.Tasks;

namespace ChatLake.Core.Services;

public interface IRawArtifactService
{
    Task<long> AddJsonArtifactAsync(
        long importBatchId,
        string artifactType,
        string artifactName,
        string jsonPayload);
}
