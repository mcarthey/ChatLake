using ChatLake.Infrastructure.Gold.Services;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Tests.Core.Gold;

public sealed class InferenceRunServiceTests : IDisposable
{
    private readonly ChatLakeDbContext _db;
    private readonly InferenceRunService _service;
    private readonly byte[] _testHash = new byte[32];

    public InferenceRunServiceTests()
    {
        var options = new DbContextOptionsBuilder<ChatLakeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new ChatLakeDbContext(options);
        _service = new InferenceRunService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task StartRunAsync_CreatesRunWithRunningStatus()
    {
        // Act
        var runId = await _service.StartRunAsync(
            runType: "Clustering",
            modelName: "ChatLake.KMeans.v1",
            modelVersion: "1.0.0",
            inputScope: "All",
            featureConfigHash: _testHash,
            inputDescription: "Test run");

        // Assert
        var run = await _db.InferenceRuns.SingleAsync();
        Assert.Equal(runId, run.InferenceRunId);
        Assert.Equal("Clustering", run.RunType);
        Assert.Equal("ChatLake.KMeans.v1", run.ModelName);
        Assert.Equal("1.0.0", run.ModelVersion);
        Assert.Equal("All", run.InputScope);
        Assert.Equal("Test run", run.InputDescription);
        Assert.Equal("Running", run.Status);
        Assert.Null(run.CompletedAtUtc);
    }

    [Fact]
    public async Task CompleteRunAsync_SetsStatusToCompleted()
    {
        // Arrange
        var runId = await _service.StartRunAsync(
            "Topics", "ChatLake.TfIdf.v1", "1.0.0", "All", _testHash);

        // Act
        await _service.CompleteRunAsync(runId, "{\"topicCount\": 15}");

        // Assert
        var run = await _db.InferenceRuns.SingleAsync();
        Assert.Equal("Completed", run.Status);
        Assert.NotNull(run.CompletedAtUtc);
        Assert.Equal("{\"topicCount\": 15}", run.MetricsJson);
    }

    [Fact]
    public async Task FailRunAsync_SetsStatusToFailed()
    {
        // Arrange
        var runId = await _service.StartRunAsync(
            "Similarity", "ChatLake.Cosine.v1", "1.0.0", "All", _testHash);

        // Act
        await _service.FailRunAsync(runId, "Out of memory");

        // Assert
        var run = await _db.InferenceRuns.SingleAsync();
        Assert.Equal("Failed", run.Status);
        Assert.NotNull(run.CompletedAtUtc);
        Assert.Contains("Out of memory", run.MetricsJson);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsDto()
    {
        // Arrange
        var runId = await _service.StartRunAsync(
            "Drift", "ChatLake.KL.v1", "1.0.0", "Project", _testHash, "Project 42");

        // Act
        var dto = await _service.GetRunAsync(runId);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(runId, dto.InferenceRunId);
        Assert.Equal("Drift", dto.RunType);
        Assert.Equal("ChatLake.KL.v1", dto.ModelName);
        Assert.Equal("Project 42", dto.InputDescription);
        Assert.Equal("Running", dto.Status);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsNullForNonExistent()
    {
        // Act
        var dto = await _service.GetRunAsync(999);

        // Assert
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetRecentRunsAsync_ReturnsOrderedByStartTime()
    {
        // Arrange - Create runs with different start times
        await _service.StartRunAsync("Clustering", "v1", "1.0", "All", _testHash);
        await Task.Delay(10); // Ensure different timestamps
        await _service.StartRunAsync("Topics", "v1", "1.0", "All", _testHash);
        await Task.Delay(10);
        await _service.StartRunAsync("Similarity", "v1", "1.0", "All", _testHash);

        // Act
        var runs = await _service.GetRecentRunsAsync();

        // Assert - Most recent first
        Assert.Equal(3, runs.Count);
        Assert.Equal("Similarity", runs[0].RunType);
        Assert.Equal("Topics", runs[1].RunType);
        Assert.Equal("Clustering", runs[2].RunType);
    }

    [Fact]
    public async Task GetRecentRunsAsync_FiltersbyRunType()
    {
        // Arrange
        await _service.StartRunAsync("Clustering", "v1", "1.0", "All", _testHash);
        await _service.StartRunAsync("Topics", "v1", "1.0", "All", _testHash);
        await _service.StartRunAsync("Clustering", "v1", "1.0", "All", _testHash);

        // Act
        var runs = await _service.GetRecentRunsAsync(runType: "Clustering");

        // Assert
        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.Equal("Clustering", r.RunType));
    }

    [Fact]
    public async Task GetRecentRunsAsync_RespectsLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _service.StartRunAsync("Clustering", "v1", "1.0", "All", _testHash);
        }

        // Act
        var runs = await _service.GetRecentRunsAsync(limit: 5);

        // Assert
        Assert.Equal(5, runs.Count);
    }

    [Fact]
    public async Task FullLifecycle_StartCompleteGetRun()
    {
        // Arrange & Act - Full lifecycle
        var runId = await _service.StartRunAsync(
            "BlogTopics", "ChatLake.Arc.v1", "2.0.0", "All", _testHash, "Full test");

        var beforeComplete = await _service.GetRunAsync(runId);
        Assert.Equal("Running", beforeComplete!.Status);

        await _service.CompleteRunAsync(runId, "{\"suggestions\": 5}");

        var afterComplete = await _service.GetRunAsync(runId);
        Assert.Equal("Completed", afterComplete!.Status);
        Assert.NotNull(afterComplete.CompletedAtUtc);
    }

    [Theory]
    [InlineData("Clustering")]
    [InlineData("Topics")]
    [InlineData("Similarity")]
    [InlineData("Drift")]
    [InlineData("BlogTopics")]
    public async Task StartRunAsync_SupportsAllRunTypes(string runType)
    {
        // Act
        var runId = await _service.StartRunAsync(
            runType, "TestModel", "1.0", "All", _testHash);

        // Assert
        var run = await _service.GetRunAsync(runId);
        Assert.NotNull(run);
        Assert.Equal(runType, run.RunType);
    }
}
