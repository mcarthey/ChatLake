using ChatLake.Core.Models;

namespace ChatLake.Core.Services;

/// <summary>
/// Service for evaluating clusters and generating blog content.
/// </summary>
public interface IBlogSuggestionService
{
    /// <summary>
    /// Evaluate accepted clusters and generate blog posts for high-scoring ones.
    /// </summary>
    Task<BlogGenerationResult> EvaluateAndGenerateAsync(
        BlogGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Regenerate content for a specific blog suggestion.
    /// </summary>
    Task<BlogTopicSuggestionDto> RegenerateContentAsync(
        long blogTopicSuggestionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-evaluate scores for a specific blog suggestion (without regenerating content).
    /// </summary>
    Task<BlogTopicSuggestionDto> ReEvaluateAsync(
        long blogTopicSuggestionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all pending blog suggestions.
    /// </summary>
    Task<IReadOnlyList<BlogTopicSuggestionDto>> GetPendingSuggestionsAsync();

    /// <summary>
    /// Get blog suggestions by status.
    /// </summary>
    Task<IReadOnlyList<BlogTopicSuggestionDto>> GetSuggestionsByStatusAsync(string status);

    /// <summary>
    /// Get detailed information for a blog suggestion including full content.
    /// </summary>
    Task<BlogTopicSuggestionDetailDto?> GetSuggestionDetailAsync(long id);

    /// <summary>
    /// Approve a blog suggestion for publishing.
    /// </summary>
    Task ApproveAsync(long id);

    /// <summary>
    /// Dismiss a blog suggestion.
    /// </summary>
    Task DismissAsync(long id);

    /// <summary>
    /// Export blog content to markdown file.
    /// </summary>
    Task<string> ExportToMarkdownAsync(long id);
}

/// <summary>
/// Summary DTO for blog topic suggestions list view.
/// </summary>
public sealed record BlogTopicSuggestionDto(
    long BlogTopicSuggestionId,
    string Title,
    decimal OverallScore,
    decimal EducationalValue,
    decimal ProblemSolvingDepth,
    decimal TopicCoherence,
    decimal ContentCompleteness,
    decimal ReaderInterest,
    string? Reasoning,
    int? WordCount,
    string Status,
    DateTime? GeneratedAtUtc,
    long? ProjectSuggestionId,
    string? ClusterName);

/// <summary>
/// Detailed DTO for blog topic suggestion including full content.
/// </summary>
public sealed record BlogTopicSuggestionDetailDto(
    long BlogTopicSuggestionId,
    string Title,
    string? BlogContentMarkdown,
    string? OutlineJson,
    decimal OverallScore,
    decimal EducationalValue,
    decimal ProblemSolvingDepth,
    decimal TopicCoherence,
    decimal ContentCompleteness,
    decimal ReaderInterest,
    string? Reasoning,
    int? WordCount,
    string Status,
    DateTime? GeneratedAtUtc,
    long? ProjectSuggestionId,
    string? ClusterName,
    IReadOnlyList<long> SourceConversationIds,
    IReadOnlyList<long> SourceSegmentIds);
