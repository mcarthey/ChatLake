using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> entity)
    {
        entity.ToTable("Project");

        entity.HasKey(e => e.ProjectId);

        entity.Property(e => e.ProjectKey)
            .IsRequired()
            .HasMaxLength(200);

        entity.HasIndex(e => e.ProjectKey)
            .IsUnique()
            .HasDatabaseName("UQ_Project_ProjectKey");

        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.Description)
            .HasMaxLength(2000);

        entity.Property(e => e.CreatedBy)
            .HasMaxLength(200);

        entity.Property(e => e.IsSystemGenerated)
            .IsRequired();

        entity.Property(e => e.IsActive)
            .IsRequired();

        entity.Property(e => e.CreatedAtUtc)
            .IsRequired();
    }
}
