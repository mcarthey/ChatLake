using HdbscanSharp.Distance;
using HdbscanSharp.Runner;

namespace ChatLake.Inference.Clustering;

/// <summary>
/// Clustering pipeline using HDBSCAN (Hierarchical Density-Based Spatial Clustering).
/// Unlike KMeans, HDBSCAN:
/// - Doesn't require specifying the number of clusters
/// - Can identify noise points that don't belong to any cluster
/// - Finds clusters of varying densities
/// </summary>
public sealed class HdbscanClusteringPipeline
{
    /// <summary>
    /// Cluster segments using pre-computed embeddings with HDBSCAN.
    /// </summary>
    /// <param name="embeddings">Pre-computed embeddings to cluster</param>
    /// <param name="minClusterSize">Minimum number of points to form a cluster (default: 5)</param>
    /// <param name="minPoints">Number of points in neighborhood for core points (default: 5)</param>
    /// <returns>Clustering result with cluster assignments and noise identification</returns>
    public HdbscanClusteringResult Cluster(
        IReadOnlyList<EmbeddingInput> embeddings,
        int minClusterSize = 5,
        int minPoints = 5)
    {
        if (embeddings.Count == 0)
        {
            return new HdbscanClusteringResult(
                Predictions: [],
                ClusterStats: [],
                NoiseSegmentIds: [],
                ClusterCount: 0);
        }

        // Convert to format expected by HdbscanSharp (double[][])
        var dataset = embeddings
            .Select(e => e.Features.Select(f => (double)f).ToArray())
            .ToArray();

        // Run HDBSCAN with cosine similarity (better for text embeddings)
        var result = HdbscanRunner.Run(
            dataset.Length,
            minPoints,
            minClusterSize,
            GenericCosineSimilarity.GetFunc(dataset));

        // Build predictions and identify noise
        var predictions = new List<HdbscanPrediction>();
        var noiseSegmentIds = new List<long>();

        for (int i = 0; i < embeddings.Count; i++)
        {
            var segmentId = embeddings[i].ConversationId; // This is actually segment ID
            var clusterId = result.Labels[i];

            // OutlierScore is a struct with Score property, default to 0 if not available
            double outlierScore = 0;
            if (result.OutliersScore != null && i < result.OutliersScore.Count)
            {
                outlierScore = result.OutliersScore[i].Score;
            }

            if (clusterId == -1 || clusterId == 0) // HDBSCAN uses -1 or 0 for noise depending on version
            {
                noiseSegmentIds.Add(segmentId);
            }

            predictions.Add(new HdbscanPrediction(
                SegmentId: segmentId,
                ClusterId: clusterId,
                OutlierScore: outlierScore));
        }

        // Calculate cluster statistics (excluding noise)
        var clusterStats = CalculateClusterStats(predictions, result.Labels);

        return new HdbscanClusteringResult(
            Predictions: predictions,
            ClusterStats: clusterStats,
            NoiseSegmentIds: noiseSegmentIds,
            ClusterCount: clusterStats.Count);
    }

    private static IReadOnlyList<HdbscanClusterStats> CalculateClusterStats(
        List<HdbscanPrediction> predictions,
        int[] labels)
    {
        var stats = new List<HdbscanClusterStats>();

        // Find unique cluster IDs (excluding noise: -1 or 0)
        var uniqueClusterIds = labels
            .Where(l => l > 0)
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        foreach (var clusterId in uniqueClusterIds)
        {
            var clusterMembers = predictions
                .Where(p => p.ClusterId == clusterId)
                .ToList();

            if (clusterMembers.Count == 0)
                continue;

            // Average outlier score (lower = more core/stable)
            var avgOutlierScore = clusterMembers.Average(p => p.OutlierScore);

            // Convert outlier score to confidence (inverse relationship)
            // Outlier scores typically range 0-1, with lower being better
            var confidence = Math.Max(0, 1.0 - avgOutlierScore);

            var segmentIds = clusterMembers
                .Select(p => p.SegmentId)
                .ToArray();

            stats.Add(new HdbscanClusterStats(
                ClusterId: clusterId,
                Count: clusterMembers.Count,
                SegmentIds: segmentIds,
                Confidence: (decimal)Math.Round(confidence, 4),
                AvgOutlierScore: avgOutlierScore));
        }

        return stats;
    }
}

/// <summary>
/// Result from HDBSCAN clustering.
/// </summary>
public sealed record HdbscanClusteringResult(
    IReadOnlyList<HdbscanPrediction> Predictions,
    IReadOnlyList<HdbscanClusterStats> ClusterStats,
    IReadOnlyList<long> NoiseSegmentIds,
    int ClusterCount);

/// <summary>
/// Individual segment prediction from HDBSCAN.
/// </summary>
public sealed record HdbscanPrediction(
    long SegmentId,
    int ClusterId,
    double OutlierScore);

/// <summary>
/// Statistics for a single HDBSCAN cluster.
/// </summary>
public sealed record HdbscanClusterStats(
    int ClusterId,
    int Count,
    IReadOnlyList<long> SegmentIds,
    decimal Confidence,
    double AvgOutlierScore);
