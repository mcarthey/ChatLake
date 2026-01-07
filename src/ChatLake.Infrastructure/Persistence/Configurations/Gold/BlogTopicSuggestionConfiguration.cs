using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public class BlogTopicSuggestionConfiguration : IEntityTypeConfiguration<BlogTopicSuggestion>
{
    public void Configure(EntityTypeBuilder<BlogTopicSuggestion> builder)
    {
        builder.ToTable("BlogTopicSuggestion");

        builder.HasKey(e => e.BlogTopicSuggestionId);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.Confidence)
            .HasColumnType("decimal(5,4)");

        builder.Property(e => e.SourceConversationIdsJson)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);

        // New fields for blog content generation
        builder.Property(e => e.BlogContentMarkdown);

        builder.Property(e => e.EvaluationScoreJson);

        builder.Property(e => e.SourceSegmentIdsJson);

        builder.Property(e => e.WordCount);

        builder.Property(e => e.GeneratedAtUtc);

        // Relationships
        builder.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ProjectSuggestion)
            .WithMany()
            .HasForeignKey(e => e.ProjectSuggestionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_BlogTopicSuggestion_Status");

        builder.HasIndex(e => e.Confidence)
            .HasDatabaseName("IX_BlogTopicSuggestion_Confidence");

        builder.HasIndex(e => e.ProjectSuggestionId)
            .HasDatabaseName("IX_BlogTopicSuggestion_ProjectSuggestionId");
    }
}
