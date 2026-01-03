using System.Security.Cryptography;
using System.Text;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Importing.Services;

public class RawArtifactService : IRawArtifactService
{
    private readonly ChatLakeDbContext _dbContext;

    public RawArtifactService(ChatLakeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<long> AddJsonArtifactAsync(
        long importBatchId,
        string artifactType,
        string artifactName,
        string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(artifactType))
            throw new ArgumentException("Artifact type is required.", nameof(artifactType));

        if (string.IsNullOrWhiteSpace(artifactName))
            throw new ArgumentException("Artifact name is required.", nameof(artifactName));

        if (jsonPayload is null)
            throw new ArgumentNullException(nameof(jsonPayload));

        // Ensure ImportBatch exists
        var batchExists = await _dbContext.ImportBatches
            .AnyAsync(b => b.ImportBatchId == importBatchId);

        if (!batchExists)
            throw new InvalidOperationException(
                $"ImportBatch with ID {importBatchId} does not exist.");

        var payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
        var sha256 = ComputeSha256(payloadBytes);

        var artifact = new RawArtifact
        {
            ImportBatchId = importBatchId,
            ArtifactType = artifactType,
            ArtifactName = artifactName,
            ContentType = "application/json",
            RawJson = jsonPayload,
            ByteLength = payloadBytes.Length,
            Sha256 = sha256,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.RawArtifacts.Add(artifact);
        await _dbContext.SaveChangesAsync();

        return artifact.RawArtifactId;
    }

    private static byte[] ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }
}
