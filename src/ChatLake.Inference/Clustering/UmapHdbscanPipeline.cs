using HdbscanSharp.Distance;
using HdbscanSharp.Runner;
using UMAP;

namespace ChatLake.Inference.Clustering;

/// <summary>
/// Wrapper to provide seeded random values to UMAP.
/// </summary>
public sealed class SeededRandomProvider : IProvideRandomValues
{
    private readonly Random _random;

    public SeededRandomProvider(int seed) => _random = new Random(seed);

    public bool IsThreadSafe => false;

    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);

    public float NextFloat() => (float)_random.NextDouble();

    public void NextFloats(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = (float)_random.NextDouble();
    }
}

/// <summary>
/// Industry-standard clustering pipeline using UMAP dimensionality reduction + HDBSCAN.
/// This is the same approach used by BERTopic and other production semantic clustering systems.
///
/// Why this works:
/// - UMAP preserves local semantic structure while reducing 768D → 15D
/// - HDBSCAN finds density-based clusters in lower-dimensional space
/// - Cosine similarity throughout maintains semantic meaning
/// </summary>
public sealed class UmapHdbscanPipeline
{
    /// <summary>
    /// Cluster segments using embeddings with UMAP reduction + HDBSCAN clustering.
    /// </summary>
    /// <param name="embeddings">768-dimensional embeddings to cluster</param>
    /// <param name="options">Pipeline configuration options</param>
    /// <param name="progressCallback">Optional progress callback (0.0 to 1.0)</param>
    /// <returns>Clustering results with cluster assignments and noise identification</returns>
    public UmapHdbscanResult Cluster(
        IReadOnlyList<EmbeddingInput> embeddings,
        UmapHdbscanOptions? options = null,
        Action<float>? progressCallback = null)
    {
        options ??= new UmapHdbscanOptions();

        if (embeddings.Count == 0)
        {
            return new UmapHdbscanResult(
                Predictions: [],
                ClusterStats: [],
                NoiseSegmentIds: [],
                ClusterCount: 0,
                UmapDimensions: options.UmapDimensions);
        }

        // Need minimum data points for UMAP to work
        if (embeddings.Count < options.UmapNeighbors)
        {
            Console.WriteLine($"[UMAP] Warning: Only {embeddings.Count} points, need at least {options.UmapNeighbors} for UMAP. Using direct HDBSCAN.");
            return ClusterDirectHdbscan(embeddings, options);
        }

        // Phase 1: UMAP dimensionality reduction (768D → 15D)
        Console.WriteLine($"[UMAP] Reducing {embeddings.Count} embeddings from 768D to {options.UmapDimensions}D...");
        var reducedEmbeddings = ReduceWithUmap(embeddings, options, progressCallback);
        Console.WriteLine($"[UMAP] Reduction complete");

        // Phase 2: HDBSCAN clustering in reduced space
        Console.WriteLine($"[HDBSCAN] Clustering in {options.UmapDimensions}D space (minClusterSize={options.MinClusterSize}, minPoints={options.MinPoints})...");
        var result = ClusterWithHdbscan(embeddings, reducedEmbeddings, options);
        Console.WriteLine($"[HDBSCAN] Found {result.ClusterCount} clusters, {result.NoiseSegmentIds.Count} noise points");

        return result;
    }

    private static float[][] ReduceWithUmap(
        IReadOnlyList<EmbeddingInput> embeddings,
        UmapHdbscanOptions options,
        Action<float>? progressCallback)
    {
        // Convert to format expected by UMAP
        var vectors = embeddings
            .Select(e => e.Features)
            .ToArray();

        // Create UMAP with cosine distance for text embeddings
        var umap = new Umap(
            distance: Umap.DistanceFunctions.Cosine,
            dimensions: options.UmapDimensions,
            numberOfNeighbors: options.UmapNeighbors,
            random: new SeededRandomProvider(options.RandomSeed)); // Deterministic for reproducibility

        // Initialize and run optimization
        var numberOfEpochs = umap.InitializeFit(vectors);

        for (var epoch = 0; epoch < numberOfEpochs; epoch++)
        {
            umap.Step();

            // Report progress every 10% or so
            if (progressCallback != null && epoch % Math.Max(1, numberOfEpochs / 10) == 0)
            {
                progressCallback((float)epoch / numberOfEpochs * 0.8f); // UMAP is 80% of total work
            }
        }

        progressCallback?.Invoke(0.8f);
        return umap.GetEmbedding();
    }

    private static UmapHdbscanResult ClusterWithHdbscan(
        IReadOnlyList<EmbeddingInput> originalEmbeddings,
        float[][] reducedEmbeddings,
        UmapHdbscanOptions options)
    {
        // Convert to format expected by HdbscanSharp
        var dataset = reducedEmbeddings
            .Select(e => e.Select(f => (double)f).ToArray())
            .ToArray();

        // Run HDBSCAN with Euclidean distance in reduced space
        // (Euclidean works well after UMAP because UMAP optimizes for it)
        var result = HdbscanRunner.Run(
            dataset.Length,
            options.MinPoints,
            options.MinClusterSize,
            GenericEuclideanDistance.GetFunc(dataset));

        // Build predictions and identify noise
        var predictions = new List<UmapHdbscanPrediction>();
        var noiseSegmentIds = new List<long>();

        for (int i = 0; i < originalEmbeddings.Count; i++)
        {
            var segmentId = originalEmbeddings[i].ConversationId; // Actually segment ID
            var clusterId = result.Labels[i];

            // Get outlier score if available
            double outlierScore = 0;
            if (result.OutliersScore != null && i < result.OutliersScore.Count)
            {
                outlierScore = result.OutliersScore[i].Score;
            }

            // HDBSCAN marks noise as -1 or 0 depending on version
            if (clusterId <= 0)
            {
                noiseSegmentIds.Add(segmentId);
            }

            predictions.Add(new UmapHdbscanPrediction(
                SegmentId: segmentId,
                ClusterId: clusterId,
                OutlierScore: outlierScore,
                ReducedEmbedding: reducedEmbeddings[i]));
        }

        // Calculate cluster statistics
        var clusterStats = CalculateClusterStats(predictions, result.Labels);

        return new UmapHdbscanResult(
            Predictions: predictions,
            ClusterStats: clusterStats,
            NoiseSegmentIds: noiseSegmentIds,
            ClusterCount: clusterStats.Count,
            UmapDimensions: reducedEmbeddings.FirstOrDefault()?.Length ?? 0);
    }

    /// <summary>
    /// Fallback for when we don't have enough points for UMAP.
    /// </summary>
    private static UmapHdbscanResult ClusterDirectHdbscan(
        IReadOnlyList<EmbeddingInput> embeddings,
        UmapHdbscanOptions options)
    {
        var dataset = embeddings
            .Select(e => e.Features.Select(f => (double)f).ToArray())
            .ToArray();

        var result = HdbscanRunner.Run(
            dataset.Length,
            options.MinPoints,
            options.MinClusterSize,
            GenericCosineSimilarity.GetFunc(dataset));

        var predictions = new List<UmapHdbscanPrediction>();
        var noiseSegmentIds = new List<long>();

        for (int i = 0; i < embeddings.Count; i++)
        {
            var segmentId = embeddings[i].ConversationId;
            var clusterId = result.Labels[i];

            double outlierScore = 0;
            if (result.OutliersScore != null && i < result.OutliersScore.Count)
            {
                outlierScore = result.OutliersScore[i].Score;
            }

            if (clusterId <= 0)
            {
                noiseSegmentIds.Add(segmentId);
            }

            predictions.Add(new UmapHdbscanPrediction(
                SegmentId: segmentId,
                ClusterId: clusterId,
                OutlierScore: outlierScore,
                ReducedEmbedding: []));
        }

        var clusterStats = CalculateClusterStats(predictions, result.Labels);

        return new UmapHdbscanResult(
            Predictions: predictions,
            ClusterStats: clusterStats,
            NoiseSegmentIds: noiseSegmentIds,
            ClusterCount: clusterStats.Count,
            UmapDimensions: 0);
    }

    private static IReadOnlyList<UmapHdbscanClusterStats> CalculateClusterStats(
        List<UmapHdbscanPrediction> predictions,
        int[] labels)
    {
        var stats = new List<UmapHdbscanClusterStats>();

        // Find unique cluster IDs (excluding noise: <= 0)
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
            var confidence = Math.Max(0, 1.0 - avgOutlierScore);

            var segmentIds = clusterMembers
                .Select(p => p.SegmentId)
                .ToArray();

            stats.Add(new UmapHdbscanClusterStats(
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
/// Configuration options for UMAP + HDBSCAN pipeline.
/// </summary>
public sealed record UmapHdbscanOptions
{
    // UMAP parameters

    /// <summary>
    /// Target dimensionality after UMAP reduction.
    /// Lower = faster clustering, but may lose nuance.
    /// BERTopic uses 5, we use 15 for more granular topics.
    /// </summary>
    public int UmapDimensions { get; init; } = 15;

    /// <summary>
    /// Number of neighbors for UMAP local structure.
    /// Lower = more local structure, higher = more global structure.
    /// </summary>
    public int UmapNeighbors { get; init; } = 15;

    // HDBSCAN parameters

    /// <summary>
    /// Minimum number of segments to form a cluster.
    /// </summary>
    public int MinClusterSize { get; init; } = 5;

    /// <summary>
    /// Number of neighbors for core point determination.
    /// </summary>
    public int MinPoints { get; init; } = 3;

    /// <summary>
    /// Random seed for reproducibility.
    /// </summary>
    public int RandomSeed { get; init; } = 42;
}

/// <summary>
/// Result from UMAP + HDBSCAN clustering.
/// </summary>
public sealed record UmapHdbscanResult(
    IReadOnlyList<UmapHdbscanPrediction> Predictions,
    IReadOnlyList<UmapHdbscanClusterStats> ClusterStats,
    IReadOnlyList<long> NoiseSegmentIds,
    int ClusterCount,
    int UmapDimensions);

/// <summary>
/// Individual segment prediction from the pipeline.
/// </summary>
public sealed record UmapHdbscanPrediction(
    long SegmentId,
    int ClusterId,
    double OutlierScore,
    float[] ReducedEmbedding);

/// <summary>
/// Statistics for a single cluster.
/// </summary>
public sealed record UmapHdbscanClusterStats(
    int ClusterId,
    int Count,
    IReadOnlyList<long> SegmentIds,
    decimal Confidence,
    double AvgOutlierScore);
