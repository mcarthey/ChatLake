using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Projects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public sealed class ConversationSummaryConfiguration : IEntityTypeConfiguration<ConversationSummary>
{
    public void Configure(EntityTypeBuilder<ConversationSummary> entity)
    {
        entity.ToTable("ConversationSummary");

        // PK is also FK -> Conversation
        entity.HasKey(e => e.ConversationId);

        entity.HasOne<Conversation>()
            .WithOne()
            .HasForeignKey<ConversationSummary>(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(e => e.MessageCount)
            .IsRequired();

        entity.Property(e => e.ParticipantSet)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(e => e.PreviewText)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(e => e.UpdatedAtUtc)
            .IsRequired();
    }
}
