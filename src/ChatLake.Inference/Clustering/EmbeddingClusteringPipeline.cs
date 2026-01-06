using Microsoft.ML;
using Microsoft.ML.Data;

namespace ChatLake.Inference.Clustering;

/// <summary>
/// Clustering pipeline that uses pre-computed semantic embeddings.
/// </summary>
public sealed class EmbeddingClusteringPipeline
{
    private readonly MLContext _mlContext;

    public EmbeddingClusteringPipeline(int? seed = null)
    {
        _mlContext = seed.HasValue ? new MLContext(seed.Value) : new MLContext();
    }

    /// <summary>
    /// Cluster conversations using pre-computed embeddings.
    /// </summary>
    /// <param name="embeddings">Pre-computed embeddings to cluster</param>
    /// <param name="clusterCount">Target number of clusters</param>
    /// <param name="maxIterations">Maximum KMeans iterations</param>
    /// <param name="outlierThreshold">Exclude segments with distance above this threshold (0 = no filtering)</param>
    public ClusteringPipelineResult Cluster(
        IReadOnlyList<EmbeddingInput> embeddings,
        int clusterCount,
        int maxIterations = 100,
        float outlierThreshold = 0)
    {
        if (embeddings.Count == 0)
        {
            return new ClusteringPipelineResult(
                Predictions: [],
                ClusterStats: [],
                Model: null!);
        }

        // Ensure we don't have more clusters than data points
        clusterCount = Math.Min(clusterCount, embeddings.Count);

        // Load embeddings into ML.NET
        var data = _mlContext.Data.LoadFromEnumerable(embeddings);

        // Train KMeans model on pre-computed embeddings
        var pipeline = _mlContext.Clustering.Trainers.KMeans(
            featureColumnName: nameof(EmbeddingInput.Features),
            numberOfClusters: clusterCount);

        var model = pipeline.Fit(data);
        var predictions = model.Transform(data);

        // Extract predictions
        var results = _mlContext.Data
            .CreateEnumerable<EmbeddingClusterPrediction>(predictions, reuseRowObject: false)
            .ToList();

        // Build ClusterPrediction list to match existing interface
        var clusterPredictions = new List<ClusterPrediction>();
        for (int i = 0; i < embeddings.Count; i++)
        {
            clusterPredictions.Add(new ClusterPrediction
            {
                ConversationId = embeddings[i].ConversationId,
                PredictedClusterId = results[i].PredictedClusterId,
                Distances = results[i].Distances
            });
        }

        // Calculate cluster statistics with outlier filtering
        var clusterStats = CalculateClusterStats(clusterPredictions, clusterCount, outlierThreshold);

        return new ClusteringPipelineResult(
            Predictions: clusterPredictions,
            ClusterStats: clusterStats,
            Model: null!); // No transformer for embedding-based clustering
    }

    private static IReadOnlyList<ClusterStats> CalculateClusterStats(
        List<ClusterPrediction> predictions,
        int clusterCount,
        float outlierThreshold)
    {
        var stats = new List<ClusterStats>();

        // If outlier threshold is set, calculate the threshold dynamically if needed
        // or use the provided value
        var effectiveThreshold = outlierThreshold;
        if (outlierThreshold <= 0)
        {
            // No filtering - use a very high threshold
            effectiveThreshold = float.MaxValue;
        }

        for (uint clusterId = 1; clusterId <= clusterCount; clusterId++)
        {
            var clusterMembers = predictions
                .Where(p => p.PredictedClusterId == clusterId)
                .ToList();

            if (clusterMembers.Count == 0)
            {
                stats.Add(new ClusterStats(clusterId, 0, Array.Empty<long>(), 0, 0));
                continue;
            }

            // Filter out outliers - members whose distance to centroid exceeds threshold
            var filteredMembers = clusterMembers
                .Where(p => (p.Distances?.Min() ?? 0) <= effectiveThreshold)
                .ToList();

            var outlierCount = clusterMembers.Count - filteredMembers.Count;

            if (filteredMembers.Count == 0)
            {
                // All members were outliers - skip this cluster
                stats.Add(new ClusterStats(clusterId, 0, Array.Empty<long>(), 0, outlierCount));
                continue;
            }

            // Calculate average distance to centroid (lower = tighter cluster = higher confidence)
            var avgDistance = filteredMembers.Average(p => p.Distances?.Min() ?? 0);

            // Convert distance to confidence (inverse relationship)
            var confidence = Math.Max(0, 1.0 - (avgDistance / 10.0));

            var conversationIds = filteredMembers
                .Select(p => p.ConversationId)
                .ToArray();

            stats.Add(new ClusterStats(
                ClusterId: clusterId,
                Count: filteredMembers.Count,
                ConversationIds: conversationIds,
                Confidence: (decimal)Math.Round(confidence, 4),
                OutlierCount: outlierCount));
        }

        return stats;
    }
}

/// <summary>
/// Input for embedding-based clustering.
/// </summary>
public sealed class EmbeddingInput
{
    public long ConversationId { get; set; }

    [VectorType(768)] // nomic-embed-text produces 768-dimensional vectors
    public float[] Features { get; set; } = [];
}

/// <summary>
/// KMeans cluster prediction output for embedding-based clustering.
/// </summary>
internal sealed class EmbeddingClusterPrediction
{
    [ColumnName("PredictedLabel")]
    public uint PredictedClusterId { get; set; }

    [ColumnName("Score")]
    public float[]? Distances { get; set; }
}
