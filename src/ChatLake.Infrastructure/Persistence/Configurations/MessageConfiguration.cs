using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> entity)
    {
        entity.ToTable("Message");

        entity.HasKey(e => e.MessageId);

        entity.Property(e => e.Role)
              .IsRequired()
              .HasMaxLength(20);

        entity.Property(e => e.Content)
              .IsRequired();

        entity.Property(e => e.ContentHash)
              .IsRequired()
              .HasColumnType("binary(32)");

        entity.HasOne<Conversation>()
              .WithMany()
              .HasForeignKey(e => e.ConversationId)
              .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<RawArtifact>()
              .WithMany()
              .HasForeignKey(e => e.RawArtifactId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.ConversationId)
              .HasDatabaseName("IX_Message_ConversationId");

        entity.HasIndex(e => e.RawArtifactId)
              .HasDatabaseName("IX_Message_RawArtifactId");

        entity.HasIndex(e => new { e.ConversationId, e.Role, e.SequenceIndex, e.ContentHash })
              .IsUnique()
              .HasDatabaseName("UX_Message_Conversation_Role_Sequence_ContentHash");
    }
}
