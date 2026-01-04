using ChatLake.Core.Services;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

public sealed class InferenceRunService : IInferenceRunService
{
    private readonly ChatLakeDbContext _db;

    public InferenceRunService(ChatLakeDbContext db)
    {
        _db = db;
    }

    public async Task<long> StartRunAsync(
        string runType,
        string modelName,
        string modelVersion,
        string inputScope,
        byte[] featureConfigHash,
        string? inputDescription = null)
    {
        var run = new InferenceRun
        {
            RunType = runType,
            ModelName = modelName,
            ModelVersion = modelVersion,
            InputScope = inputScope,
            FeatureConfigHashSha256 = featureConfigHash,
            InputDescription = inputDescription,
            StartedAtUtc = DateTime.UtcNow,
            Status = "Running"
        };

        _db.InferenceRuns.Add(run);
        await _db.SaveChangesAsync();

        return run.InferenceRunId;
    }

    public async Task CompleteRunAsync(long runId, string? metricsJson = null)
    {
        var run = await _db.InferenceRuns.SingleAsync(r => r.InferenceRunId == runId);

        run.Status = "Completed";
        run.CompletedAtUtc = DateTime.UtcNow;
        run.MetricsJson = metricsJson;

        await _db.SaveChangesAsync();
    }

    public async Task FailRunAsync(long runId, string errorMessage)
    {
        var run = await _db.InferenceRuns.SingleAsync(r => r.InferenceRunId == runId);

        run.Status = "Failed";
        run.CompletedAtUtc = DateTime.UtcNow;
        run.MetricsJson = System.Text.Json.JsonSerializer.Serialize(new { error = errorMessage });

        await _db.SaveChangesAsync();
    }

    public async Task<InferenceRunDto?> GetRunAsync(long runId)
    {
        return await _db.InferenceRuns
            .Where(r => r.InferenceRunId == runId)
            .Select(r => ToDto(r))
            .SingleOrDefaultAsync();
    }

    public async Task<IReadOnlyList<InferenceRunDto>> GetRecentRunsAsync(string? runType = null, int limit = 20)
    {
        var query = _db.InferenceRuns.AsQueryable();

        if (!string.IsNullOrEmpty(runType))
            query = query.Where(r => r.RunType == runType);

        return await query
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(limit)
            .Select(r => ToDto(r))
            .ToListAsync();
    }

    private static InferenceRunDto ToDto(InferenceRun r) => new(
        r.InferenceRunId,
        r.RunType,
        r.ModelName,
        r.ModelVersion,
        r.InputScope,
        r.InputDescription,
        r.StartedAtUtc,
        r.CompletedAtUtc,
        r.Status,
        r.MetricsJson);
}
