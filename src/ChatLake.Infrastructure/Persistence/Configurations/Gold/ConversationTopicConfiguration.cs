using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public class ConversationTopicConfiguration : IEntityTypeConfiguration<ConversationTopic>
{
    public void Configure(EntityTypeBuilder<ConversationTopic> builder)
    {
        builder.ToTable("ConversationTopic");

        builder.HasKey(e => e.ConversationTopicId);

        builder.Property(e => e.Score)
            .HasColumnType("decimal(7,6)");

        builder.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Topic)
            .WithMany()
            .HasForeignKey(e => e.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ConversationId)
            .HasDatabaseName("IX_ConversationTopic_ConversationId");

        builder.HasIndex(e => e.TopicId)
            .HasDatabaseName("IX_ConversationTopic_TopicId");
    }
}
