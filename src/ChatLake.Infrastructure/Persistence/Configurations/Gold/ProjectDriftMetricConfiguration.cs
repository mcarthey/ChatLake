using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public class ProjectDriftMetricConfiguration : IEntityTypeConfiguration<ProjectDriftMetric>
{
    public void Configure(EntityTypeBuilder<ProjectDriftMetric> builder)
    {
        builder.ToTable("ProjectDriftMetric");

        builder.HasKey(e => e.ProjectDriftMetricId);

        builder.Property(e => e.DriftScore)
            .HasColumnType("decimal(7,6)");

        builder.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ProjectId, e.WindowStartUtc })
            .HasDatabaseName("IX_ProjectDriftMetric_Project_Window");
    }
}
