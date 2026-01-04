using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public class ConversationSimilarityConfiguration : IEntityTypeConfiguration<ConversationSimilarity>
{
    public void Configure(EntityTypeBuilder<ConversationSimilarity> builder)
    {
        builder.ToTable("ConversationSimilarity");

        builder.HasKey(e => e.ConversationSimilarityId);

        builder.Property(e => e.Similarity)
            .HasColumnType("decimal(7,6)");

        builder.Property(e => e.Method)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint on (InferenceRunId, ConversationIdA, ConversationIdB)
        builder.HasIndex(e => new { e.InferenceRunId, e.ConversationIdA, e.ConversationIdB })
            .IsUnique()
            .HasDatabaseName("UQ_ConversationSimilarity_Pair");

        builder.HasIndex(e => e.ConversationIdA)
            .HasDatabaseName("IX_ConversationSimilarity_A");

        builder.HasIndex(e => e.ConversationIdB)
            .HasDatabaseName("IX_ConversationSimilarity_B");

        builder.HasIndex(e => e.Similarity)
            .HasDatabaseName("IX_ConversationSimilarity_Value");
    }
}
