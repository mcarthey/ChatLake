using System.Security.Cryptography;
using System.Text;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChatLake.Infrastructure.Importing.Services;

public class RawArtifactService : IRawArtifactService
{
    private readonly ChatLakeDbContext _dbContext;
    private readonly string _artifactStoragePath;

    public RawArtifactService(ChatLakeDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _artifactStoragePath = configuration["ArtifactStorage:BasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "artifacts");
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

    public async Task<long> AddStreamArtifactAsync(
        long importBatchId,
        string artifactType,
        string artifactName,
        Stream content,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artifactType))
            throw new ArgumentException("Artifact type is required.", nameof(artifactType));

        if (string.IsNullOrWhiteSpace(artifactName))
            throw new ArgumentException("Artifact name is required.", nameof(artifactName));

        if (content is null)
            throw new ArgumentNullException(nameof(content));

        // Ensure ImportBatch exists
        var batchExists = await _dbContext.ImportBatches
            .AnyAsync(b => b.ImportBatchId == importBatchId, ct);

        if (!batchExists)
            throw new InvalidOperationException(
                $"ImportBatch with ID {importBatchId} does not exist.");

        // Create storage directory structure: artifacts/{batchId}/
        var batchDir = Path.Combine(_artifactStoragePath, importBatchId.ToString());
        Directory.CreateDirectory(batchDir);

        // Generate unique filename
        var safeFileName = Path.GetFileName(artifactName);
        var storedPath = Path.Combine(batchDir, $"{Guid.NewGuid():N}_{safeFileName}");

        // Stream to file while computing SHA256
        long byteLength;
        byte[] sha256;

        using (var sha = SHA256.Create())
        using (var fileStream = new FileStream(storedPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, useAsync: true))
        using (var cryptoStream = new CryptoStream(fileStream, sha, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(cryptoStream, ct);
            await cryptoStream.FlushFinalBlockAsync(ct);

            byteLength = fileStream.Length;
            sha256 = sha.Hash!;
        }

        var artifact = new RawArtifact
        {
            ImportBatchId = importBatchId,
            ArtifactType = artifactType,
            ArtifactName = artifactName,
            ContentType = "application/json",
            StoredPath = storedPath,
            RawJson = null, // Not storing in DB - using file
            ByteLength = byteLength,
            Sha256 = sha256,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.RawArtifacts.Add(artifact);
        await _dbContext.SaveChangesAsync(ct);

        return artifact.RawArtifactId;
    }
}
