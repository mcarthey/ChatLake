using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public sealed class ProjectConversationConfiguration : IEntityTypeConfiguration<ProjectConversation>
{
    public void Configure(EntityTypeBuilder<ProjectConversation> entity)
    {
        entity.ToTable("ProjectConversation", t =>
        {
            t.HasCheckConstraint("CK_ProjectConversation_AddedBy", "[AddedBy] IN ('Manual','System')");
        });

        entity.HasKey(e => new { e.ProjectId, e.ConversationId });

        entity.Property(e => e.AddedBy)
            .IsRequired()
            .HasMaxLength(20);

        entity.Property(e => e.AddedAtUtc)
            .IsRequired();

        // FK -> Project
        entity.HasOne<Project>()
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK -> Conversation (Silver)
        entity.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.ConversationId)
            .HasDatabaseName("IX_ProjectConversation_ConversationId");
    }
}
