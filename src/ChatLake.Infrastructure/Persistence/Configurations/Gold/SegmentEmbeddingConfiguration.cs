using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public sealed class SegmentEmbeddingConfiguration : IEntityTypeConfiguration<SegmentEmbedding>
{
    public void Configure(EntityTypeBuilder<SegmentEmbedding> builder)
    {
        builder.ToTable("SegmentEmbedding");

        builder.HasKey(e => e.SegmentEmbeddingId);

        builder.Property(e => e.EmbeddingModel)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Dimensions)
            .IsRequired();

        // Store embedding as varbinary(max) for flexibility
        // 768 floats * 4 bytes = 3072 bytes typical
        builder.Property(e => e.EmbeddingVector)
            .IsRequired();

        builder.Property(e => e.SourceContentHash)
            .IsRequired()
            .HasColumnType("binary(32)");

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        // FK to ConversationSegment - cascade delete embeddings when segment deleted
        builder.HasOne(e => e.ConversationSegment)
            .WithMany()
            .HasForeignKey(e => e.ConversationSegmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to InferenceRun - restrict to preserve audit trail
        builder.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only one embedding per segment per model
        builder.HasIndex(e => new { e.ConversationSegmentId, e.EmbeddingModel })
            .IsUnique()
            .HasDatabaseName("UQ_SegmentEmbedding_Segment_Model");

        // Index for finding embeddings by inference run
        builder.HasIndex(e => e.InferenceRunId)
            .HasDatabaseName("IX_SegmentEmbedding_InferenceRunId");
    }
}
