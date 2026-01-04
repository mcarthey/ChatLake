using ChatLake.Inference.Similarity;

namespace ChatLake.Tests.Core.Similarity;

public class ConversationSimilarityPipelineTests
{
    [Fact]
    public void BuildVectors_WithSampleData_Succeeds()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        var conversations = CreateSampleConversations();

        // Act & Assert - should not throw
        pipeline.BuildVectors(conversations);
    }

    [Fact]
    public void BuildVectors_WithEmptyData_Succeeds()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);

        // Act & Assert - should not throw
        pipeline.BuildVectors([]);
    }

    [Fact]
    public void CalculateAllPairs_IdenticalTexts_ReturnsHighSimilarity()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        var conversations = new List<ConversationTextInput>
        {
            new() { ConversationId = 1, Text = "Machine learning with neural networks and deep learning" },
            new() { ConversationId = 2, Text = "Machine learning with neural networks and deep learning" }
        };

        pipeline.BuildVectors(conversations);

        // Act
        var pairs = pipeline.CalculateAllPairs(minSimilarity: 0.1m, maxPairsPerConversation: 10);

        // Assert
        Assert.Single(pairs);
        Assert.Equal(1m, pairs[0].Similarity); // Identical texts should have similarity 1.0
    }

    [Fact]
    public void CalculateAllPairs_SimilarTexts_ReturnsModerateSimilarity()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        var conversations = new List<ConversationTextInput>
        {
            new() { ConversationId = 1, Text = "How do I configure Entity Framework Core with SQL Server?" },
            new() { ConversationId = 2, Text = "Setting up Entity Framework Core and SQL Server connection" }
        };

        pipeline.BuildVectors(conversations);

        // Act
        var pairs = pipeline.CalculateAllPairs(minSimilarity: 0.1m, maxPairsPerConversation: 10);

        // Assert - TF-IDF gives moderate similarity for related but different texts
        Assert.Single(pairs);
        Assert.True(pairs[0].Similarity > 0.3m, $"Expected moderate similarity, got {pairs[0].Similarity}");
    }

    [Fact]
    public void CalculateAllPairs_DifferentTexts_ReturnsLowOrNoSimilarity()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        var conversations = new List<ConversationTextInput>
        {
            new() { ConversationId = 1, Text = "Machine learning with neural networks" },
            new() { ConversationId = 2, Text = "Cooking recipes for Italian pasta dishes" }
        };

        pipeline.BuildVectors(conversations);

        // Act
        var pairs = pipeline.CalculateAllPairs(minSimilarity: 0.5m, maxPairsPerConversation: 10);

        // Assert - very different texts should not meet threshold
        Assert.Empty(pairs);
    }

    [Fact]
    public void CalculateAllPairs_RespectsMinSimilarityThreshold()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        var conversations = CreateSampleConversations();
        pipeline.BuildVectors(conversations);

        // Act
        var lowThreshold = pipeline.CalculateAllPairs(minSimilarity: 0.1m, maxPairsPerConversation: 100);
        var highThreshold = pipeline.CalculateAllPairs(minSimilarity: 0.8m, maxPairsPerConversation: 100);

        // Assert
        Assert.True(lowThreshold.Count >= highThreshold.Count);
    }

    [Fact]
    public void CalculateAllPairs_EnforcesIdALessThanIdB()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        var conversations = new List<ConversationTextInput>
        {
            new() { ConversationId = 100, Text = "Same text for testing" },
            new() { ConversationId = 50, Text = "Same text for testing" }
        };

        pipeline.BuildVectors(conversations);

        // Act
        var pairs = pipeline.CalculateAllPairs(minSimilarity: 0.1m, maxPairsPerConversation: 10);

        // Assert
        Assert.Single(pairs);
        Assert.Equal(50, pairs[0].ConversationIdA);
        Assert.Equal(100, pairs[0].ConversationIdB);
    }

    [Fact]
    public void FindSimilar_ReturnsOrderedBySimilarity()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        var conversations = new List<ConversationTextInput>
        {
            new() { ConversationId = 1, Text = "Entity Framework Core database migrations" },
            new() { ConversationId = 2, Text = "Entity Framework migrations and database schema" },
            new() { ConversationId = 3, Text = "React component state management" },
            new() { ConversationId = 4, Text = "Entity Framework Core with SQL Server" }
        };

        pipeline.BuildVectors(conversations);

        // Act
        var similar = pipeline.FindSimilar("Entity Framework database setup", limit: 3);

        // Assert
        Assert.True(similar.Count > 0);
        for (int i = 1; i < similar.Count; i++)
        {
            Assert.True(similar[i - 1].Similarity >= similar[i].Similarity);
        }
    }

    [Fact]
    public void FindSimilar_RespectsLimit()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        pipeline.BuildVectors(CreateSampleConversations());

        // Act
        var similar = pipeline.FindSimilar("database query optimization", limit: 2);

        // Assert
        Assert.True(similar.Count <= 2);
    }

    [Fact]
    public void GetSimilarity_ReturnsValueBetweenZeroAndOne()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        var conversations = CreateSampleConversations();
        pipeline.BuildVectors(conversations);

        // Act
        var similarity = pipeline.GetSimilarity(1, 2);

        // Assert
        Assert.NotNull(similarity);
        Assert.InRange(similarity.Value, 0m, 1m);
    }

    [Fact]
    public void GetSimilarity_NonExistentConversation_ReturnsNull()
    {
        // Arrange
        var pipeline = new ConversationSimilarityPipeline(seed: 42);
        pipeline.BuildVectors(CreateSampleConversations());

        // Act
        var similarity = pipeline.GetSimilarity(1, 9999);

        // Assert
        Assert.Null(similarity);
    }

    [Fact]
    public void IsDeterministic_WithSameSeed()
    {
        // Arrange
        var conversations = CreateSampleConversations();

        // Act
        var pipeline1 = new ConversationSimilarityPipeline(seed: 42);
        pipeline1.BuildVectors(conversations);
        var pairs1 = pipeline1.CalculateAllPairs(0.1m, 100);

        var pipeline2 = new ConversationSimilarityPipeline(seed: 42);
        pipeline2.BuildVectors(conversations);
        var pairs2 = pipeline2.CalculateAllPairs(0.1m, 100);

        // Assert
        Assert.Equal(pairs1.Count, pairs2.Count);
        for (int i = 0; i < pairs1.Count; i++)
        {
            Assert.Equal(pairs1[i].ConversationIdA, pairs2[i].ConversationIdA);
            Assert.Equal(pairs1[i].ConversationIdB, pairs2[i].ConversationIdB);
            Assert.Equal(pairs1[i].Similarity, pairs2[i].Similarity);
        }
    }

    private static List<ConversationTextInput> CreateSampleConversations()
    {
        return new List<ConversationTextInput>
        {
            new()
            {
                ConversationId = 1,
                Text = "How do I configure Entity Framework Core with SQL Server? I need to set up migrations."
            },
            new()
            {
                ConversationId = 2,
                Text = "Entity Framework Core migration strategies and database schema updates."
            },
            new()
            {
                ConversationId = 3,
                Text = "React hooks tutorial: useState and useEffect for state management."
            },
            new()
            {
                ConversationId = 4,
                Text = "Machine learning model training with TensorFlow and neural networks."
            },
            new()
            {
                ConversationId = 5,
                Text = "SQL Server query optimization and database indexing strategies."
            }
        };
    }
}
