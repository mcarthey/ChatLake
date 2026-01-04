using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public class InferenceRunConfiguration : IEntityTypeConfiguration<InferenceRun>
{
    public void Configure(EntityTypeBuilder<InferenceRun> builder)
    {
        builder.ToTable("InferenceRun");

        builder.HasKey(e => e.InferenceRunId);

        builder.Property(e => e.RunType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.ModelName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ModelVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.FeatureConfigHashSha256)
            .IsRequired()
            .HasColumnType("binary(32)");

        builder.Property(e => e.InputScope)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.InputDescription)
            .HasMaxLength(2000);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(e => e.StartedAtUtc)
            .HasDatabaseName("IX_InferenceRun_StartedAtUtc");

        builder.HasIndex(e => e.RunType)
            .HasDatabaseName("IX_InferenceRun_RunType");
    }
}
