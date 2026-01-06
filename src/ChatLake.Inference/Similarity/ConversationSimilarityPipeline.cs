using Microsoft.ML;
using Microsoft.ML.Data;

namespace ChatLake.Inference.Similarity;

/// <summary>
/// ML.NET pipeline for calculating conversation similarity using TF-IDF.
/// </summary>
public sealed class ConversationSimilarityPipeline
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private List<ConversationVector>? _vectors;

    public ConversationSimilarityPipeline(int? seed = null)
    {
        _mlContext = seed.HasValue ? new MLContext(seed.Value) : new MLContext();
    }

    /// <summary>
    /// Build TF-IDF vectors for all conversations.
    /// Must be called before calculating similarities.
    /// </summary>
    public void BuildVectors(IEnumerable<ConversationTextInput> conversations)
    {
        var conversationList = conversations.ToList();
        if (conversationList.Count == 0)
        {
            _vectors = [];
            return;
        }

        var data = _mlContext.Data.LoadFromEnumerable(conversationList);

        // Build TF-IDF pipeline with limited vocabulary for performance
        var textOptions = new Microsoft.ML.Transforms.Text.TextFeaturizingEstimator.Options
        {
            OutputTokensColumnName = null,
            CaseMode = Microsoft.ML.Transforms.Text.TextNormalizingEstimator.CaseMode.Lower,
            KeepDiacritics = false,
            KeepPunctuations = false,
            KeepNumbers = false,
            WordFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
            {
                NgramLength = 1,
                MaximumNgramsCount = new[] { 500 } // Limit vocabulary size
            },
            CharFeatureExtractor = null // Disable character n-grams for speed
        };

        var pipeline = _mlContext.Transforms.Text.FeaturizeText(
            outputColumnName: "Features",
            options: textOptions,
            inputColumnNames: nameof(ConversationTextInput.Text));

        _model = pipeline.Fit(data);
        var transformedData = _model.Transform(data);

        // Extract feature vectors
        _vectors = _mlContext.Data
            .CreateEnumerable<ConversationVector>(transformedData, reuseRowObject: false)
            .ToList();

        if (_vectors.Count > 0 && _vectors[0].Features != null)
        {
            Console.WriteLine($"[Similarity] Vector dimensions: {_vectors[0].Features.Length}");
        }
    }

    /// <summary>
    /// Calculate similarity between all pairs above threshold.
    /// </summary>
    public IReadOnlyList<SimilarityPair> CalculateAllPairs(
        decimal minSimilarity,
        int maxPairsPerConversation)
    {
        if (_vectors == null || _vectors.Count < 2)
            return [];

        var minSim = (float)minSimilarity;
        var allPairs = new System.Collections.Concurrent.ConcurrentBag<SimilarityPair>();

        // Parallel calculation of pairwise similarities
        Parallel.For(0, _vectors.Count, i =>
        {
            for (int j = i + 1; j < _vectors.Count; j++)
            {
                var similarity = CalculateCosineSimilarity(
                    _vectors[i].Features,
                    _vectors[j].Features);

                if (similarity >= minSim)
                {
                    var idA = _vectors[i].ConversationId;
                    var idB = _vectors[j].ConversationId;

                    // Ensure IdA < IdB
                    if (idA > idB)
                        (idA, idB) = (idB, idA);

                    allPairs.Add(new SimilarityPair(idA, idB, (decimal)similarity));
                }
            }
        });

        // Apply maxPairsPerConversation limit after parallel calculation
        var pairsPerConversation = new Dictionary<long, int>();
        var result = new List<SimilarityPair>();

        foreach (var pair in allPairs.OrderByDescending(p => p.Similarity))
        {
            var countA = pairsPerConversation.GetValueOrDefault(pair.ConversationIdA, 0);
            var countB = pairsPerConversation.GetValueOrDefault(pair.ConversationIdB, 0);

            if (countA >= maxPairsPerConversation && countB >= maxPairsPerConversation)
                continue;

            result.Add(pair);
            pairsPerConversation[pair.ConversationIdA] = countA + 1;
            pairsPerConversation[pair.ConversationIdB] = countB + 1;
        }

        return result;
    }

    /// <summary>
    /// Find conversations most similar to a query text.
    /// </summary>
    public IReadOnlyList<SimilarityMatch> FindSimilar(string queryText, int limit)
    {
        if (_model == null || _vectors == null || _vectors.Count == 0)
            return [];

        // Vectorize the query
        var queryData = _mlContext.Data.LoadFromEnumerable(new[]
        {
            new ConversationTextInput { ConversationId = -1, Text = queryText }
        });

        var transformedQuery = _model.Transform(queryData);
        var queryVector = _mlContext.Data
            .CreateEnumerable<ConversationVector>(transformedQuery, reuseRowObject: false)
            .First();

        // Calculate similarities with all conversations
        var similarities = _vectors
            .Select(v => new
            {
                v.ConversationId,
                Similarity = CalculateCosineSimilarity(queryVector.Features, v.Features)
            })
            .Where(x => x.Similarity > 0)
            .OrderByDescending(x => x.Similarity)
            .Take(limit)
            .Select(x => new SimilarityMatch(x.ConversationId, (decimal)x.Similarity))
            .ToList();

        return similarities;
    }

    /// <summary>
    /// Get similarity between two specific conversations.
    /// </summary>
    public decimal? GetSimilarity(long conversationIdA, long conversationIdB)
    {
        if (_vectors == null)
            return null;

        var vecA = _vectors.FirstOrDefault(v => v.ConversationId == conversationIdA);
        var vecB = _vectors.FirstOrDefault(v => v.ConversationId == conversationIdB);

        if (vecA == null || vecB == null)
            return null;

        return (decimal)CalculateCosineSimilarity(vecA.Features, vecB.Features);
    }

    private static float CalculateCosineSimilarity(float[]? vecA, float[]? vecB)
    {
        if (vecA == null || vecB == null || vecA.Length != vecB.Length)
            return 0f;

        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;

        for (int i = 0; i < vecA.Length; i++)
        {
            dotProduct += vecA[i] * vecB[i];
            normA += vecA[i] * vecA[i];
            normB += vecB[i] * vecB[i];
        }

        if (normA == 0 || normB == 0)
            return 0f;

        return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}

/// <summary>
/// Input data for similarity pipeline.
/// </summary>
public sealed class ConversationTextInput
{
    public long ConversationId { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Conversation with TF-IDF feature vector.
/// </summary>
public sealed class ConversationVector
{
    public long ConversationId { get; set; }

    [ColumnName("Features")]
    public float[]? Features { get; set; }
}

/// <summary>
/// Similarity pair for storage.
/// </summary>
public sealed record SimilarityPair(
    long ConversationIdA,
    long ConversationIdB,
    decimal Similarity);

/// <summary>
/// Similarity match for search results.
/// </summary>
public sealed record SimilarityMatch(
    long ConversationId,
    decimal Similarity);
