namespace ChatLake.Core.Models;

/// <summary>
/// Evaluation scores for a cluster's blog-worthiness.
/// Each score ranges from 0.0 to 1.0.
/// </summary>
public record BlogEvaluationScores
{
    /// <summary>
    /// Does the content teach something useful?
    /// </summary>
    public decimal EducationalValue { get; init; }

    /// <summary>
    /// Were real problems solved with depth?
    /// </summary>
    public decimal ProblemSolvingDepth { get; init; }

    /// <summary>
    /// Is the topic focused enough for one post?
    /// </summary>
    public decimal TopicCoherence { get; init; }

    /// <summary>
    /// Is there enough material for a full post?
    /// </summary>
    public decimal ContentCompleteness { get; init; }

    /// <summary>
    /// Would developers find this interesting?
    /// </summary>
    public decimal ReaderInterest { get; init; }

    /// <summary>
    /// Combined overall score.
    /// </summary>
    public decimal OverallScore { get; init; }

    /// <summary>
    /// LLM's reasoning for the scores.
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// Calculates a weighted overall score from individual criteria.
    /// </summary>
    public static decimal CalculateOverallScore(BlogEvaluationScores scores)
    {
        // Weights: Educational and Problem-Solving are most important
        return (scores.EducationalValue * 0.25m) +
               (scores.ProblemSolvingDepth * 0.25m) +
               (scores.TopicCoherence * 0.20m) +
               (scores.ContentCompleteness * 0.15m) +
               (scores.ReaderInterest * 0.15m);
    }
}
