namespace ChatLake.Core.Services;

/// <summary>
/// Service for managing project suggestions from ML clustering.
/// </summary>
public interface IProjectSuggestionService
{
    /// <summary>
    /// Get all pending suggestions.
    /// </summary>
    Task<IReadOnlyList<ProjectSuggestionDto>> GetPendingSuggestionsAsync();

    /// <summary>
    /// Get suggestions by status.
    /// </summary>
    Task<IReadOnlyList<ProjectSuggestionDto>> GetSuggestionsByStatusAsync(string status);

    /// <summary>
    /// Accept a suggestion - creates a new project and assigns conversations.
    /// </summary>
    Task<long> AcceptSuggestionAsync(long suggestionId);

    /// <summary>
    /// Reject a suggestion - marks it as dismissed.
    /// </summary>
    Task RejectSuggestionAsync(long suggestionId);

    /// <summary>
    /// Merge a suggestion into an existing project.
    /// </summary>
    Task MergeSuggestionAsync(long suggestionId, long targetProjectId);

    /// <summary>
    /// Get conversation IDs that would be assigned if suggestion is accepted.
    /// </summary>
    Task<IReadOnlyList<ConversationPreviewDto>> GetSuggestionConversationsAsync(long suggestionId);

    /// <summary>
    /// Delete all pending suggestions.
    /// </summary>
    Task<int> ClearAllPendingAsync();
}

public sealed record ProjectSuggestionDto(
    long ProjectSuggestionId,
    long InferenceRunId,
    string SuggestedName,
    string SuggestedProjectKey,
    string? Summary,
    decimal Confidence,
    string Status,
    int SegmentCount,
    int ConversationCount,
    DateTime? ResolvedAtUtc,
    long? ResolvedProjectId);

public sealed record ConversationPreviewDto(
    long ConversationId,
    string? Title,
    DateTime? FirstMessageAtUtc,
    int MessageCount);
