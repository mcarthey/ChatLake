using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("Topic");

        builder.HasKey(e => e.TopicId);

        builder.Property(e => e.Label)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(e => e.InferenceRun)
            .WithMany()
            .HasForeignKey(e => e.InferenceRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.InferenceRunId)
            .HasDatabaseName("IX_Topic_InferenceRunId");
    }
}
