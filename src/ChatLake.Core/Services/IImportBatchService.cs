using System.Threading.Tasks;

namespace ChatLake.Core.Services;

public interface IImportBatchService
{
    Task<long> CreateAsync(
        string sourceSystem,
        string? sourceVersion,
        string? importedBy,
        string? importLabel);

    Task MarkCommittedAsync(long importBatchId, int artifactCount);

    Task MarkFailedAsync(long importBatchId, string? notes);
}
