using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public class ParsingFailureConfiguration : IEntityTypeConfiguration<ParsingFailure>
{
    public void Configure(EntityTypeBuilder<ParsingFailure> entity)
    {
        entity.ToTable("ParsingFailure");

        entity.HasKey(e => e.ParsingFailureId);

        entity.Property(e => e.FailureStage)
              .IsRequired()
              .HasMaxLength(50);

        entity.Property(e => e.FailureMessage)
              .IsRequired();

        entity.HasOne<RawArtifact>()
              .WithMany()
              .HasForeignKey(e => e.RawArtifactId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.RawArtifactId)
              .HasDatabaseName("IX_ParsingFailure_RawArtifactId");
    }
}
