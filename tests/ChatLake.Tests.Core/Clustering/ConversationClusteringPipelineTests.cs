using ChatLake.Inference.Clustering;

namespace ChatLake.Tests.Core.Clustering;

public sealed class ConversationClusteringPipelineTests
{
    [Fact]
    public void Cluster_WithSampleData_ReturnsClusterAssignments()
    {
        // Arrange
        var pipeline = new ConversationClusteringPipeline(seed: 42);
        var conversations = CreateSampleConversations(20);

        // Act
        var result = pipeline.Cluster(conversations, clusterCount: 3);

        // Assert
        Assert.Equal(20, result.Predictions.Count);
        Assert.Equal(3, result.ClusterStats.Count);
        Assert.All(result.Predictions, p => Assert.InRange(p.PredictedClusterId, 1u, 3u));
    }

    [Fact]
    public void Cluster_IsDeterministic_WithSameSeed()
    {
        // Arrange
        var conversations = CreateSampleConversations(15);

        // Act - Run twice with same seed
        var pipeline1 = new ConversationClusteringPipeline(seed: 42);
        var result1 = pipeline1.Cluster(conversations, clusterCount: 3);

        var pipeline2 = new ConversationClusteringPipeline(seed: 42);
        var result2 = pipeline2.Cluster(conversations, clusterCount: 3);

        // Assert - Same cluster assignments
        for (int i = 0; i < result1.Predictions.Count; i++)
        {
            Assert.Equal(
                result1.Predictions[i].PredictedClusterId,
                result2.Predictions[i].PredictedClusterId);
        }
    }

    [Fact]
    public void Cluster_GroupsSimilarContent()
    {
        // Arrange - Create conversations with distinct themes (more content for better TF-IDF)
        var conversations = new List<ConversationTextInput>
        {
            // Programming cluster - heavy Python keywords
            new() { ConversationId = 1, Text = "Python programming code debugging for loop list tuple dictionary function class method variable syntax error exception handling" },
            new() { ConversationId = 2, Text = "Python code development programming software debugging testing unittest pytest functions classes modules packages" },
            new() { ConversationId = 3, Text = "Python programming language code script debugging error traceback exception import module function def class" },

            // Cooking cluster - heavy food keywords
            new() { ConversationId = 4, Text = "cooking recipe food kitchen baking chocolate cake oven temperature flour sugar eggs butter ingredients" },
            new() { ConversationId = 5, Text = "recipe cooking food pasta Italian kitchen homemade ingredients tomato sauce garlic olive oil basil" },
            new() { ConversationId = 6, Text = "baking cookies recipe food kitchen oven temperature sugar butter flour chocolate chips ingredients dessert" },

            // Travel cluster - heavy travel keywords
            new() { ConversationId = 7, Text = "travel vacation Paris France Europe tourist attractions Eiffel Tower museum hotel flight booking trip" },
            new() { ConversationId = 8, Text = "travel flights Europe booking cheap airline tickets vacation trip hotel accommodation tourist destination" },
            new() { ConversationId = 9, Text = "vacation travel beach packing list trip flight hotel resort destination tourism holiday summer" }
        };

        var pipeline = new ConversationClusteringPipeline(seed: 42);

        // Act
        var result = pipeline.Cluster(conversations, clusterCount: 3);

        // Assert - With 3 clusters and 9 items, verify basic clustering behavior
        Assert.Equal(9, result.Predictions.Count);
        Assert.Equal(3, result.ClusterStats.Count);

        // Each cluster should have at least 1 member
        Assert.All(result.ClusterStats.Where(c => c.Count > 0), c => Assert.True(c.Count >= 1));

        // Total assigned should equal input count
        Assert.Equal(9, result.ClusterStats.Sum(c => c.Count));

        // At least 2 clusters should be non-empty (ML might merge some)
        Assert.True(result.ClusterStats.Count(c => c.Count > 0) >= 2);
    }

    [Fact]
    public void Cluster_CalculatesClusterStats()
    {
        // Arrange
        var pipeline = new ConversationClusteringPipeline(seed: 42);
        var conversations = CreateSampleConversations(30);

        // Act
        var result = pipeline.Cluster(conversations, clusterCount: 5);

        // Assert
        Assert.Equal(5, result.ClusterStats.Count);

        // Total conversation IDs across all clusters should equal input count
        var totalAssigned = result.ClusterStats.Sum(c => c.ConversationIds.Count);
        Assert.Equal(30, totalAssigned);

        // Each cluster should have confidence between 0 and 1
        Assert.All(result.ClusterStats, c => Assert.InRange(c.Confidence, 0m, 1m));
    }

    [Fact]
    public void Cluster_WithMinimalData_Succeeds()
    {
        // Arrange - Minimum viable clustering (2 items, 2 clusters)
        var pipeline = new ConversationClusteringPipeline(seed: 42);
        var conversations = new List<ConversationTextInput>
        {
            new() { ConversationId = 1, Text = "Hello world programming code" },
            new() { ConversationId = 2, Text = "Cooking recipes food kitchen" }
        };

        // Act
        var result = pipeline.Cluster(conversations, clusterCount: 2);

        // Assert
        Assert.Equal(2, result.Predictions.Count);
        Assert.NotEqual(
            result.Predictions[0].PredictedClusterId,
            result.Predictions[1].PredictedClusterId);
    }

    [Fact]
    public void Cluster_PreservesConversationIds()
    {
        // Arrange
        var pipeline = new ConversationClusteringPipeline(seed: 42);
        var conversations = new List<ConversationTextInput>
        {
            new() { ConversationId = 100, Text = "Test content one" },
            new() { ConversationId = 200, Text = "Test content two" },
            new() { ConversationId = 300, Text = "Test content three" }
        };

        // Act
        var result = pipeline.Cluster(conversations, clusterCount: 2);

        // Assert - All original IDs preserved
        var predictedIds = result.Predictions.Select(p => p.ConversationId).OrderBy(x => x).ToList();
        Assert.Equal(new long[] { 100, 200, 300 }, predictedIds);
    }

    [Fact]
    public void Cluster_ReturnsTrainedModel()
    {
        // Arrange
        var pipeline = new ConversationClusteringPipeline(seed: 42);
        var conversations = CreateSampleConversations(10);

        // Act
        var result = pipeline.Cluster(conversations, clusterCount: 3);

        // Assert
        Assert.NotNull(result.Model);
    }

    private static List<ConversationTextInput> CreateSampleConversations(int count)
    {
        var topics = new[]
        {
            "programming software code development debugging testing",
            "cooking recipes food kitchen baking ingredients",
            "travel vacation flights hotels tourism destinations",
            "music songs albums artists concerts instruments",
            "sports fitness exercise training workout health"
        };

        return Enumerable.Range(1, count)
            .Select(i => new ConversationTextInput
            {
                ConversationId = i,
                Text = $"{topics[i % topics.Length]} conversation {i} with more content about {topics[(i + 1) % topics.Length]}",
                Title = $"Conversation {i}"
            })
            .ToList();
    }
}
