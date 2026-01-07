namespace ChatLake.Core.Models;

/// <summary>
/// Result of the blog evaluation and generation pipeline.
/// </summary>
public record BlogGenerationResult
{
    /// <summary>
    /// Number of clusters evaluated.
    /// </summary>
    public int ClustersEvaluated { get; init; }

    /// <summary>
    /// Number of clusters that passed the threshold.
    /// </summary>
    public int ClustersPassed { get; init; }

    /// <summary>
    /// Number of blog posts generated.
    /// </summary>
    public int BlogsGenerated { get; init; }

    /// <summary>
    /// Total time taken for the pipeline.
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// IDs of the generated BlogTopicSuggestion records.
    /// </summary>
    public IReadOnlyList<long> GeneratedBlogIds { get; init; } = [];

    /// <summary>
    /// Any errors encountered during processing.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>
/// Options for the blog generation pipeline.
/// </summary>
public record BlogGenerationOptions
{
    /// <summary>
    /// Minimum overall score required to generate a blog post.
    /// </summary>
    public decimal MinOverallScore { get; init; } = 0.60m;

    /// <summary>
    /// Target word count for generated posts.
    /// </summary>
    public int TargetWordCount { get; init; } = 2000;

    /// <summary>
    /// Maximum number of clusters to evaluate per run.
    /// </summary>
    public int MaxClustersToEvaluate { get; init; } = 20;

    /// <summary>
    /// Minimum segments required for a cluster to be considered.
    /// </summary>
    public int MinSegmentCount { get; init; } = 5;
}
