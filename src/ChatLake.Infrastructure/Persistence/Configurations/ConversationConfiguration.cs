using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> entity)
    {
        entity.ToTable("Conversation");

        entity.HasKey(e => e.ConversationId);

        entity.Property(e => e.ConversationKey)
              .IsRequired()
              .HasColumnType("binary(32)");

        entity.Property(e => e.SourceSystem)
              .IsRequired()
              .HasMaxLength(50);

        entity.Property(e => e.ExternalConversationId)
              .HasMaxLength(200);

        entity.HasIndex(e => e.ConversationKey)
              .IsUnique()
              .HasDatabaseName("UX_Conversation_ConversationKey");

        entity.HasOne<ImportBatch>()
              .WithMany()
              .HasForeignKey(e => e.CreatedFromImportBatchId)
              .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.CreatedFromImportBatchId)
              .HasDatabaseName("IX_Conversation_CreatedFromImportBatchId");
    }
}
