using Microsoft.ML;
using Microsoft.ML.Data;

namespace ChatLake.Inference.Clustering;

/// <summary>
/// ML.NET pipeline for clustering conversations using TF-IDF + KMeans.
/// </summary>
public sealed class ConversationClusteringPipeline
{
    private readonly MLContext _mlContext;

    public ConversationClusteringPipeline(int? seed = null)
    {
        _mlContext = seed.HasValue ? new MLContext(seed.Value) : new MLContext();
    }

    /// <summary>
    /// Train the clustering model and predict cluster assignments.
    /// </summary>
    /// <param name="conversations">Conversations with concatenated text</param>
    /// <param name="clusterCount">Number of clusters (K)</param>
    /// <param name="maxIterations">Max KMeans iterations</param>
    /// <returns>Cluster assignments for each conversation</returns>
    public ClusteringPipelineResult Cluster(
        IEnumerable<ConversationTextInput> conversations,
        int clusterCount,
        int maxIterations = 100)
    {
        var data = _mlContext.Data.LoadFromEnumerable(conversations);

        // Build pipeline: Text -> TF-IDF Features -> KMeans
        var pipeline = _mlContext.Transforms.Text.FeaturizeText(
                outputColumnName: "Features",
                inputColumnName: nameof(ConversationTextInput.Text))
            .Append(_mlContext.Clustering.Trainers.KMeans(
                featureColumnName: "Features",
                numberOfClusters: clusterCount));

        // Train the model
        var model = pipeline.Fit(data);

        // Predict cluster assignments
        var predictions = model.Transform(data);

        // Extract results
        var predictedLabels = _mlContext.Data
            .CreateEnumerable<ClusterPrediction>(predictions, reuseRowObject: false)
            .ToList();

        // Calculate cluster statistics
        var clusterStats = CalculateClusterStats(predictedLabels, clusterCount);

        return new ClusteringPipelineResult(
            Predictions: predictedLabels,
            ClusterStats: clusterStats,
            Model: model);
    }

    private static IReadOnlyList<ClusterStats> CalculateClusterStats(
        List<ClusterPrediction> predictions,
        int clusterCount)
    {
        var stats = new List<ClusterStats>();

        for (uint clusterId = 1; clusterId <= clusterCount; clusterId++)
        {
            var clusterMembers = predictions
                .Where(p => p.PredictedClusterId == clusterId)
                .ToList();

            if (clusterMembers.Count == 0)
            {
                stats.Add(new ClusterStats(clusterId, 0, Array.Empty<long>(), 0));
                continue;
            }

            // Calculate average distance to centroid (lower = tighter cluster = higher confidence)
            var avgDistance = clusterMembers.Average(p => p.Distances?.Min() ?? 0);

            // Convert distance to confidence (inverse relationship)
            // Normalize: distance 0 = confidence 1.0, higher distance = lower confidence
            var confidence = Math.Max(0, 1.0 - (avgDistance / 10.0)); // Scale factor of 10

            var conversationIds = clusterMembers
                .Select(p => p.ConversationId)
                .ToArray();

            stats.Add(new ClusterStats(
                ClusterId: clusterId,
                Count: clusterMembers.Count,
                ConversationIds: conversationIds,
                Confidence: (decimal)Math.Round(confidence, 4)));
        }

        return stats;
    }
}

/// <summary>
/// Input data for clustering pipeline.
/// </summary>
public sealed class ConversationTextInput
{
    public long ConversationId { get; set; }

    /// <summary>
    /// Concatenated message content for the conversation.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional title from first user message for naming clusters.
    /// </summary>
    public string? Title { get; set; }
}

/// <summary>
/// Output from clustering prediction.
/// </summary>
public sealed class ClusterPrediction
{
    public long ConversationId { get; set; }

    [ColumnName("PredictedLabel")]
    public uint PredictedClusterId { get; set; }

    [ColumnName("Score")]
    public float[]? Distances { get; set; }
}

/// <summary>
/// Statistics for a single cluster.
/// </summary>
public sealed record ClusterStats(
    uint ClusterId,
    int Count,
    IReadOnlyList<long> ConversationIds,
    decimal Confidence,
    int OutlierCount = 0);

/// <summary>
/// Complete result from clustering pipeline.
/// </summary>
public sealed record ClusteringPipelineResult(
    IReadOnlyList<ClusterPrediction> Predictions,
    IReadOnlyList<ClusterStats> ClusterStats,
    ITransformer Model);
