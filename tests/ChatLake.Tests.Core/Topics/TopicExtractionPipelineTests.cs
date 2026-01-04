using ChatLake.Inference.Topics;

namespace ChatLake.Tests.Core.Topics;

public class TopicExtractionPipelineTests
{
    [Fact]
    public void ExtractTopics_WithSampleData_ReturnsTopicAssignments()
    {
        // Arrange
        var pipeline = new TopicExtractionPipeline(seed: 42);
        var conversations = CreateSampleConversations();

        // Act
        var result = pipeline.ExtractTopics(conversations, topicCount: 3);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Topics.Count);
        Assert.True(result.Assignments.Count > 0);
    }

    [Fact]
    public void ExtractTopics_WithEmptyData_ReturnsEmptyResult()
    {
        // Arrange
        var pipeline = new TopicExtractionPipeline(seed: 42);
        var conversations = new List<ConversationTextData>();

        // Act
        var result = pipeline.ExtractTopics(conversations, topicCount: 3);

        // Assert
        Assert.Empty(result.Topics);
        Assert.Empty(result.Assignments);
        Assert.Null(result.Model);
    }

    [Fact]
    public void ExtractTopics_IsDeterministic_WithSameSeed()
    {
        // Arrange
        var conversations = CreateSampleConversations();

        // Act - run twice with same seed
        var pipeline1 = new TopicExtractionPipeline(seed: 42);
        var result1 = pipeline1.ExtractTopics(conversations, topicCount: 3);

        var pipeline2 = new TopicExtractionPipeline(seed: 42);
        var result2 = pipeline2.ExtractTopics(conversations, topicCount: 3);

        // Assert - assignments should be identical
        Assert.Equal(result1.Assignments.Count, result2.Assignments.Count);

        for (int i = 0; i < result1.Assignments.Count; i++)
        {
            Assert.Equal(result1.Assignments[i].ConversationId, result2.Assignments[i].ConversationId);
            Assert.Equal(result1.Assignments[i].TopicIndex, result2.Assignments[i].TopicIndex);
            // Scores should be very close (floating point tolerance)
            Assert.True(Math.Abs(result1.Assignments[i].Score - result2.Assignments[i].Score) < 0.0001m);
        }
    }

    [Fact]
    public void ExtractTopics_AssignmentsHaveValidScores()
    {
        // Arrange
        var pipeline = new TopicExtractionPipeline(seed: 42);
        var conversations = CreateSampleConversations();

        // Act
        var result = pipeline.ExtractTopics(conversations, topicCount: 3);

        // Assert - all scores should be between 0 and 1
        foreach (var assignment in result.Assignments)
        {
            Assert.InRange(assignment.Score, 0m, 1m);
        }
    }

    [Fact]
    public void ExtractTopics_EachConversationHasAssignmentsForAllTopics()
    {
        // Arrange
        var pipeline = new TopicExtractionPipeline(seed: 42);
        var conversations = CreateSampleConversations();
        var topicCount = 3;

        // Act
        var result = pipeline.ExtractTopics(conversations, topicCount);

        // Assert - each conversation should have assignments for all topics
        var conversationIds = conversations.Select(c => c.ConversationId).ToList();
        foreach (var convId in conversationIds)
        {
            var convAssignments = result.Assignments.Where(a => a.ConversationId == convId).ToList();
            Assert.Equal(topicCount, convAssignments.Count);
        }
    }

    [Fact]
    public void ExtractTopics_TopicScoresAreDistributed()
    {
        // Arrange
        var pipeline = new TopicExtractionPipeline(seed: 42);
        var conversations = CreateSampleConversations();

        // Act
        var result = pipeline.ExtractTopics(conversations, topicCount: 3);

        // Assert - for each conversation, topic scores should sum to approximately 1
        var conversationIds = conversations.Select(c => c.ConversationId).Distinct();
        foreach (var convId in conversationIds)
        {
            var convScores = result.Assignments
                .Where(a => a.ConversationId == convId)
                .Sum(a => a.Score);

            // LDA distributions should sum to 1 (with some tolerance)
            Assert.InRange(convScores, 0.99m, 1.01m);
        }
    }

    [Fact]
    public void ExtractTopics_PreservesConversationIds()
    {
        // Arrange
        var pipeline = new TopicExtractionPipeline(seed: 42);
        var conversations = new List<ConversationTextData>
        {
            new() { ConversationId = 100, Text = "Machine learning and neural networks" },
            new() { ConversationId = 200, Text = "Database optimization and SQL queries" },
            new() { ConversationId = 300, Text = "Frontend development with React" }
        };

        // Act
        var result = pipeline.ExtractTopics(conversations, topicCount: 2);

        // Assert
        var assignedConvIds = result.Assignments.Select(a => a.ConversationId).Distinct().ToList();
        Assert.Contains(100L, assignedConvIds);
        Assert.Contains(200L, assignedConvIds);
        Assert.Contains(300L, assignedConvIds);
    }

    private static List<ConversationTextData> CreateSampleConversations()
    {
        return new List<ConversationTextData>
        {
            new()
            {
                ConversationId = 1,
                Text = "How do I configure Entity Framework Core with SQL Server? I need to set up migrations and connection strings for my database."
            },
            new()
            {
                ConversationId = 2,
                Text = "What are the best practices for React hooks? I want to understand useState and useEffect better for my frontend application."
            },
            new()
            {
                ConversationId = 3,
                Text = "Can you explain machine learning model training? I'm working on a classification problem with neural networks."
            },
            new()
            {
                ConversationId = 4,
                Text = "I need help with database indexing and query optimization. My SQL queries are running slowly."
            },
            new()
            {
                ConversationId = 5,
                Text = "How to implement Redux state management in a React application? I need global state for my components."
            },
            new()
            {
                ConversationId = 6,
                Text = "Deep learning with TensorFlow and PyTorch. Training convolutional neural networks for image classification."
            },
            new()
            {
                ConversationId = 7,
                Text = "Entity Framework migrations and database schema updates. How to handle production database changes."
            },
            new()
            {
                ConversationId = 8,
                Text = "React component lifecycle and performance optimization. Using memo and useCallback hooks."
            }
        };
    }
}
