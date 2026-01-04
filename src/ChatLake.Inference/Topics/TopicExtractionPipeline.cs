using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace ChatLake.Inference.Topics;

/// <summary>
/// ML.NET pipeline for extracting topics from conversations using LDA.
/// </summary>
public sealed class TopicExtractionPipeline
{
    private readonly MLContext _mlContext;

    public TopicExtractionPipeline(int? seed = null)
    {
        _mlContext = seed.HasValue ? new MLContext(seed.Value) : new MLContext();
    }

    /// <summary>
    /// Extract topics from conversations using Latent Dirichlet Allocation.
    /// </summary>
    /// <param name="conversations">Conversations with text content</param>
    /// <param name="topicCount">Number of topics to extract</param>
    /// <param name="keywordsPerTopic">Number of keywords per topic</param>
    /// <returns>Topic extraction results with assignments</returns>
    public TopicExtractionPipelineResult ExtractTopics(
        IEnumerable<ConversationTextData> conversations,
        int topicCount,
        int keywordsPerTopic = 10)
    {
        var conversationList = conversations.ToList();
        if (conversationList.Count == 0)
        {
            return new TopicExtractionPipelineResult(
                Topics: [],
                Assignments: [],
                Model: null);
        }

        var data = _mlContext.Data.LoadFromEnumerable(conversationList);

        // Build pipeline: Text -> Tokens -> Bag of Words -> LDA
        var textPipeline = _mlContext.Transforms.Text.NormalizeText(
                outputColumnName: "NormalizedText",
                inputColumnName: nameof(ConversationTextData.Text),
                caseMode: TextNormalizingEstimator.CaseMode.Lower,
                keepDiacritics: false,
                keepPunctuations: false,
                keepNumbers: true)
            .Append(_mlContext.Transforms.Text.TokenizeIntoWords(
                outputColumnName: "Tokens",
                inputColumnName: "NormalizedText"))
            .Append(_mlContext.Transforms.Text.RemoveDefaultStopWords(
                outputColumnName: "FilteredTokens",
                inputColumnName: "Tokens"))
            .Append(_mlContext.Transforms.Conversion.MapValueToKey(
                outputColumnName: "TokenKeys",
                inputColumnName: "FilteredTokens"))
            .Append(_mlContext.Transforms.Text.ProduceNgrams(
                outputColumnName: "BagOfWords",
                inputColumnName: "TokenKeys",
                ngramLength: 1,
                useAllLengths: false,
                weighting: NgramExtractingEstimator.WeightingCriteria.Tf))
            .Append(_mlContext.Transforms.Text.LatentDirichletAllocation(
                outputColumnName: "TopicDistribution",
                inputColumnName: "BagOfWords",
                numberOfTopics: topicCount,
                maximumNumberOfIterations: 100,
                resetRandomGenerator: true));

        // Train the model
        var model = textPipeline.Fit(data);

        // Transform data to get topic distributions
        var transformedData = model.Transform(data);

        // Extract topic assignments for each conversation
        var assignments = ExtractAssignments(transformedData, conversationList, topicCount);

        // Extract topic keywords from the model
        var topics = ExtractTopicKeywords(model, topicCount, keywordsPerTopic);

        return new TopicExtractionPipelineResult(
            Topics: topics,
            Assignments: assignments,
            Model: model);
    }

    private List<TopicAssignment> ExtractAssignments(
        IDataView transformedData,
        List<ConversationTextData> originalData,
        int topicCount)
    {
        var assignments = new List<TopicAssignment>();

        // Get topic distributions
        var distributions = _mlContext.Data
            .CreateEnumerable<TopicDistributionOutput>(transformedData, reuseRowObject: false)
            .ToList();

        for (int i = 0; i < distributions.Count && i < originalData.Count; i++)
        {
            var dist = distributions[i];
            var conversationId = originalData[i].ConversationId;

            if (dist.TopicDistribution == null || dist.TopicDistribution.Length == 0)
                continue;

            // Create assignments for each topic with its score
            for (int topicIndex = 0; topicIndex < dist.TopicDistribution.Length && topicIndex < topicCount; topicIndex++)
            {
                var score = dist.TopicDistribution[topicIndex];
                assignments.Add(new TopicAssignment(
                    ConversationId: conversationId,
                    TopicIndex: topicIndex,
                    Score: (decimal)score));
            }
        }

        return assignments;
    }

    private List<ExtractedTopic> ExtractTopicKeywords(
        ITransformer model,
        int topicCount,
        int keywordsPerTopic)
    {
        var topics = new List<ExtractedTopic>();

        // LDA doesn't directly expose topic-word distributions in ML.NET
        // We generate placeholder labels that can be refined later
        // In a production system, you might post-process with additional analysis

        for (int i = 0; i < topicCount; i++)
        {
            // Generate initial label - can be refined by looking at top documents
            var label = $"Topic {i + 1}";

            // Keywords would ideally come from the LDA model's topic-word matrix
            // ML.NET doesn't directly expose this, so we leave empty for now
            // The service layer can compute keywords from high-scoring documents
            var keywords = new List<string>();

            topics.Add(new ExtractedTopic(
                TopicIndex: i,
                Label: label,
                Keywords: keywords));
        }

        return topics;
    }
}

/// <summary>
/// Input data for topic extraction.
/// </summary>
public sealed class ConversationTextData
{
    public long ConversationId { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Output with topic distribution.
/// </summary>
public sealed class TopicDistributionOutput
{
    [ColumnName("TopicDistribution")]
    public float[]? TopicDistribution { get; set; }
}

/// <summary>
/// Topic assignment for a conversation.
/// </summary>
public sealed record TopicAssignment(
    long ConversationId,
    int TopicIndex,
    decimal Score);

/// <summary>
/// Extracted topic with keywords.
/// </summary>
public sealed record ExtractedTopic(
    int TopicIndex,
    string Label,
    IReadOnlyList<string> Keywords);

/// <summary>
/// Complete result from topic extraction pipeline.
/// </summary>
public sealed record TopicExtractionPipelineResult(
    IReadOnlyList<ExtractedTopic> Topics,
    IReadOnlyList<TopicAssignment> Assignments,
    ITransformer? Model);
