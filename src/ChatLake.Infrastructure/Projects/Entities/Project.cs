namespace ChatLake.Infrastructure.Projects.Entities;

/// <summary>
/// Projects are curated groupings of conversations.
/// Can be user-created or system-suggested.
/// </summary>
public class Project
{
    public long ProjectId { get; set; }

    /// <summary>
    /// URL-friendly slug identifier.
    /// </summary>
    public string ProjectKey { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who created this: username or "System"
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Reserved for system-created projects via clustering.
    /// </summary>
    public bool IsSystemGenerated { get; set; }

    public bool IsActive { get; set; } = true;
}
