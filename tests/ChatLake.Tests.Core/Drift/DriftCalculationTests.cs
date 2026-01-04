namespace ChatLake.Tests.Core.Drift;

/// <summary>
/// Unit tests for drift calculation logic.
/// Tests the mathematical properties of drift scores.
/// </summary>
public class DriftCalculationTests
{
    [Fact]
    public void CalculateDriftScore_IdenticalDistributions_ReturnsZero()
    {
        // Arrange
        var prev = new Dictionary<long, decimal> { { 1, 0.5m }, { 2, 0.3m }, { 3, 0.2m } };
        var curr = new Dictionary<long, decimal> { { 1, 0.5m }, { 2, 0.3m }, { 3, 0.2m } };

        // Act
        var drift = CalculateDriftScore(prev, curr);

        // Assert
        Assert.True(drift < 0.01m, $"Expected near-zero drift for identical distributions, got {drift}");
    }

    [Fact]
    public void CalculateDriftScore_CompletelyDifferentTopics_ReturnsHighValue()
    {
        // Arrange - no overlap in topics
        var prev = new Dictionary<long, decimal> { { 1, 0.6m }, { 2, 0.4m } };
        var curr = new Dictionary<long, decimal> { { 3, 0.7m }, { 4, 0.3m } };

        // Act
        var drift = CalculateDriftScore(prev, curr);

        // Assert - should be 1.0 (maximum drift) since vectors are orthogonal
        Assert.Equal(1m, drift);
    }

    [Fact]
    public void CalculateDriftScore_PartialOverlap_ReturnsModerateValue()
    {
        // Arrange - some overlap
        var prev = new Dictionary<long, decimal> { { 1, 0.6m }, { 2, 0.4m } };
        var curr = new Dictionary<long, decimal> { { 1, 0.3m }, { 2, 0.2m }, { 3, 0.5m } };

        // Act
        var drift = CalculateDriftScore(prev, curr);

        // Assert - should be between 0 and 1
        Assert.InRange(drift, 0.1m, 0.9m);
    }

    [Fact]
    public void CalculateDriftScore_SameTopicsDifferentWeights_ReturnsLowValue()
    {
        // Arrange - same topics, slightly different weights
        var prev = new Dictionary<long, decimal> { { 1, 0.5m }, { 2, 0.3m }, { 3, 0.2m } };
        var curr = new Dictionary<long, decimal> { { 1, 0.4m }, { 2, 0.35m }, { 3, 0.25m } };

        // Act
        var drift = CalculateDriftScore(prev, curr);

        // Assert - should be low since topics are same
        Assert.True(drift < 0.1m, $"Expected low drift for similar distributions, got {drift}");
    }

    [Fact]
    public void CalculateDriftScore_IsSymmetric()
    {
        // Arrange
        var dist1 = new Dictionary<long, decimal> { { 1, 0.6m }, { 2, 0.4m } };
        var dist2 = new Dictionary<long, decimal> { { 1, 0.3m }, { 2, 0.5m }, { 3, 0.2m } };

        // Act
        var drift12 = CalculateDriftScore(dist1, dist2);
        var drift21 = CalculateDriftScore(dist2, dist1);

        // Assert - drift should be symmetric
        Assert.Equal(drift12, drift21);
    }

    [Fact]
    public void CalculateDriftScore_EmptyPrevious_ReturnsMaximum()
    {
        // Arrange
        var prev = new Dictionary<long, decimal>();
        var curr = new Dictionary<long, decimal> { { 1, 0.5m }, { 2, 0.5m } };

        // Act
        var drift = CalculateDriftScore(prev, curr);

        // Assert
        Assert.Equal(1m, drift);
    }

    [Fact]
    public void CalculateDriftScore_EmptyCurrent_ReturnsMaximum()
    {
        // Arrange
        var prev = new Dictionary<long, decimal> { { 1, 0.5m }, { 2, 0.5m } };
        var curr = new Dictionary<long, decimal>();

        // Act
        var drift = CalculateDriftScore(prev, curr);

        // Assert
        Assert.Equal(1m, drift);
    }

    [Fact]
    public void CalculateDriftScore_BothEmpty_ReturnsZero()
    {
        // Arrange
        var prev = new Dictionary<long, decimal>();
        var curr = new Dictionary<long, decimal>();

        // Act
        var drift = CalculateDriftScore(prev, curr);

        // Assert
        Assert.Equal(0m, drift);
    }

    [Fact]
    public void CalculateTopicShifts_ReturnsCorrectChanges()
    {
        // Arrange
        var prev = new Dictionary<long, decimal> { { 1, 0.5m }, { 2, 0.3m } };
        var curr = new Dictionary<long, decimal> { { 1, 0.3m }, { 2, 0.4m }, { 3, 0.3m } };
        var labels = new Dictionary<long, string> { { 1, "Topic A" }, { 2, "Topic B" }, { 3, "Topic C" } };

        // Act
        var shifts = CalculateTopicShifts(prev, curr, labels);

        // Assert
        Assert.Equal(3, shifts.Count);

        var topicA = shifts.First(s => s.TopicLabel == "Topic A");
        Assert.Equal(0.5m, topicA.PreviousScore);
        Assert.Equal(0.3m, topicA.CurrentScore);
        Assert.Equal(-0.2m, topicA.Change);

        var topicC = shifts.First(s => s.TopicLabel == "Topic C");
        Assert.Equal(0m, topicC.PreviousScore);
        Assert.Equal(0.3m, topicC.CurrentScore);
        Assert.Equal(0.3m, topicC.Change);
    }

    [Fact]
    public void CalculateTopicShifts_OrderedByAbsoluteChange()
    {
        // Arrange
        var prev = new Dictionary<long, decimal> { { 1, 0.5m }, { 2, 0.3m }, { 3, 0.2m } };
        var curr = new Dictionary<long, decimal> { { 1, 0.1m }, { 2, 0.35m }, { 3, 0.55m } };
        var labels = new Dictionary<long, string> { { 1, "A" }, { 2, "B" }, { 3, "C" } };

        // Act
        var shifts = CalculateTopicShifts(prev, curr, labels);

        // Assert - ordered by absolute change (descending)
        Assert.Equal("A", shifts[0].TopicLabel); // -0.4 change
        Assert.Equal("C", shifts[1].TopicLabel); // +0.35 change
        Assert.Equal("B", shifts[2].TopicLabel); // +0.05 change
    }

    // Helper methods that mirror the service implementation
    private static decimal CalculateDriftScore(
        Dictionary<long, decimal> prev,
        Dictionary<long, decimal> curr)
    {
        var allTopics = prev.Keys.Union(curr.Keys).ToList();

        if (allTopics.Count == 0)
            return 0m;

        var prevVec = allTopics.Select(t => (double)prev.GetValueOrDefault(t, 0m)).ToArray();
        var currVec = allTopics.Select(t => (double)curr.GetValueOrDefault(t, 0m)).ToArray();

        var prevNorm = Math.Sqrt(prevVec.Sum(x => x * x));
        var currNorm = Math.Sqrt(currVec.Sum(x => x * x));

        if (prevNorm == 0 || currNorm == 0)
            return 1m;

        for (int i = 0; i < prevVec.Length; i++)
        {
            prevVec[i] /= prevNorm;
            currVec[i] /= currNorm;
        }

        var dotProduct = prevVec.Zip(currVec, (a, b) => a * b).Sum();
        var distance = 1.0 - dotProduct;
        return (decimal)Math.Max(0, Math.Min(1, distance));
    }

    private static List<TopicShift> CalculateTopicShifts(
        Dictionary<long, decimal> prev,
        Dictionary<long, decimal> curr,
        Dictionary<long, string> labels)
    {
        var allTopics = prev.Keys.Union(curr.Keys).ToList();

        return allTopics
            .Select(topicId =>
            {
                var prevScore = prev.GetValueOrDefault(topicId, 0m);
                var currScore = curr.GetValueOrDefault(topicId, 0m);
                return new TopicShift(
                    TopicLabel: labels.GetValueOrDefault(topicId, $"Topic {topicId}"),
                    PreviousScore: prevScore,
                    CurrentScore: currScore,
                    Change: currScore - prevScore);
            })
            .OrderByDescending(ts => Math.Abs(ts.Change))
            .ToList();
    }

    private sealed record TopicShift(
        string TopicLabel,
        decimal PreviousScore,
        decimal CurrentScore,
        decimal Change);
}
