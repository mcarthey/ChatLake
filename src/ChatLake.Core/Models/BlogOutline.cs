namespace ChatLake.Core.Models;

/// <summary>
/// Structured outline for a blog post.
/// </summary>
public record BlogOutline
{
    /// <summary>
    /// Hook or opening paragraph summary.
    /// </summary>
    public string? Hook { get; init; }

    /// <summary>
    /// Sections of the blog post in order.
    /// </summary>
    public IReadOnlyList<BlogSection> Sections { get; init; } = [];

    /// <summary>
    /// Key takeaways or conclusion points.
    /// </summary>
    public IReadOnlyList<string> KeyTakeaways { get; init; } = [];

    /// <summary>
    /// Suggested code examples to include.
    /// </summary>
    public IReadOnlyList<string> CodeExamples { get; init; } = [];
}

/// <summary>
/// A section within a blog post outline.
/// </summary>
public record BlogSection
{
    /// <summary>
    /// Section heading.
    /// </summary>
    public string Heading { get; init; } = null!;

    /// <summary>
    /// Key points to cover in this section.
    /// </summary>
    public IReadOnlyList<string> KeyPoints { get; init; } = [];

    /// <summary>
    /// Estimated word count for this section.
    /// </summary>
    public int? EstimatedWordCount { get; init; }
}
