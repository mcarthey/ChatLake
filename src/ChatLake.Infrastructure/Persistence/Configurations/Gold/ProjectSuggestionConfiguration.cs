using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public class ProjectSuggestionConfiguration : IEntityTypeConfiguration<ProjectSuggestion>
{
    public void Configure(EntityTypeBuilder<ProjectSuggestion> builder)
    {
        builder.ToTable("ProjectSuggestion");

        builder.HasKey(e => e.ProjectSuggestionId);

        builder.Property(e => e.SuggestedProjectKey)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.SuggestedName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Summary)
            .HasMaxLength(2000);

        builder.Property(e => e.Confidence)
            .HasColumnType("decimal(5,4)");

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ResolvedProject)
            .WithMany()
            .HasForeignKey(e => e.ResolvedProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_ProjectSuggestion_Status");

        builder.HasIndex(e => e.Confidence)
            .HasDatabaseName("IX_ProjectSuggestion_Confidence");
    }
}
