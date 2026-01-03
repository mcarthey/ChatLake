using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public class ConversationArtifactMapConfiguration : IEntityTypeConfiguration<ConversationArtifactMap>
{
    public void Configure(EntityTypeBuilder<ConversationArtifactMap> entity)
    {
        entity.ToTable("ConversationArtifactMap");

        entity.HasKey(e => new { e.ConversationId, e.RawArtifactId });

        entity.HasOne<Conversation>()
              .WithMany()
              .HasForeignKey(e => e.ConversationId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<RawArtifact>()
              .WithMany()
              .HasForeignKey(e => e.RawArtifactId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.RawArtifactId)
              .HasDatabaseName("IX_ConversationArtifactMap_RawArtifactId");
    }
}
