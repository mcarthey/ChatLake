using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public sealed class ProjectConversationConfiguration : IEntityTypeConfiguration<ProjectConversation>
{
    public void Configure(EntityTypeBuilder<ProjectConversation> entity)
    {
        entity.ToTable("ProjectConversation");

        entity.HasKey(e => e.ProjectConversationId);

        entity.Property(e => e.AssignedBy)
            .IsRequired()
            .HasMaxLength(20);

        entity.Property(e => e.Confidence)
            .HasColumnType("decimal(5,4)");

        entity.Property(e => e.AssignedAtUtc)
            .IsRequired();

        entity.Property(e => e.IsCurrent)
            .IsRequired();

        // FK -> Project
        entity.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK -> Conversation (Silver)
        entity.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK -> InferenceRun (optional)
        entity.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.ProjectId)
            .HasDatabaseName("IX_ProjectConversation_ProjectId");

        entity.HasIndex(e => e.ConversationId)
            .HasDatabaseName("IX_ProjectConversation_ConversationId");

        // Filtered unique index: only one current assignment per project+conversation
        entity.HasIndex(e => new { e.ProjectId, e.ConversationId, e.IsCurrent })
            .HasFilter("[IsCurrent] = 1")
            .IsUnique()
            .HasDatabaseName("UQ_ProjectConversation_Current");
    }
}
