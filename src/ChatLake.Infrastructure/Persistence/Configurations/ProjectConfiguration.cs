using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> entity)
    {
        entity.ToTable("Project", t =>
        {
            t.HasCheckConstraint("CK_Project_Status", "[Status] IN ('Active','Archived')");
        });

        entity.HasKey(e => e.ProjectId);

        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("UX_Project_Name");

        entity.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);

        entity.Property(e => e.IsSystemGenerated)
            .IsRequired();

        entity.Property(e => e.CreatedAtUtc)
            .IsRequired();

        entity.Property(e => e.UpdatedAtUtc)
            .IsRequired();
    }
}
