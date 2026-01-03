namespace ChatLake.Infrastructure.Projects.Entities;

public class Project
{
    public long ProjectId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Active | Archived</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Reserved for future system-created projects.</summary>
    public bool IsSystemGenerated { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
