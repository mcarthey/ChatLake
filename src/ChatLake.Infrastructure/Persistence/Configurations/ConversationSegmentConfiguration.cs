using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public sealed class ConversationSegmentConfiguration : IEntityTypeConfiguration<ConversationSegment>
{
    public void Configure(EntityTypeBuilder<ConversationSegment> builder)
    {
        builder.ToTable("ConversationSegment");

        builder.HasKey(e => e.ConversationSegmentId);

        builder.Property(e => e.SegmentIndex)
            .IsRequired();

        builder.Property(e => e.StartMessageIndex)
            .IsRequired();

        builder.Property(e => e.EndMessageIndex)
            .IsRequired();

        builder.Property(e => e.MessageCount)
            .IsRequired();

        builder.Property(e => e.ContentText)
            .IsRequired();

        builder.Property(e => e.ContentHash)
            .IsRequired()
            .HasColumnType("binary(32)");

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        // FK to Conversation - cascade delete segments when conversation deleted
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to InferenceRun - restrict to preserve audit trail
        builder.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index for fast lookup by conversation
        builder.HasIndex(e => e.ConversationId)
            .HasDatabaseName("IX_ConversationSegment_ConversationId");

        // Unique constraint: one segment per index per conversation
        builder.HasIndex(e => new { e.ConversationId, e.SegmentIndex })
            .IsUnique()
            .HasDatabaseName("UQ_ConversationSegment_Position");

        // Index for finding segments by inference run
        builder.HasIndex(e => e.InferenceRunId)
            .HasDatabaseName("IX_ConversationSegment_InferenceRunId");
    }
}
