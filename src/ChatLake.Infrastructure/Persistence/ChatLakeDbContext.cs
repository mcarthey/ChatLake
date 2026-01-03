using ChatLake.Infrastructure.Importing.Entities;
using Microsoft.EntityFrameworkCore;
using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Persistence.Configurations;

namespace ChatLake.Infrastructure.Persistence;

public class ChatLakeDbContext : DbContext
{
    public ChatLakeDbContext(DbContextOptions<ChatLakeDbContext> options)
        : base(options)
    {
    }

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<RawArtifact> RawArtifacts => Set<RawArtifact>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ConversationArtifactMap> ConversationArtifactMaps => Set<ConversationArtifactMap>();
    public DbSet<ParsingFailure> ParsingFailures => Set<ParsingFailure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureImportBatch(modelBuilder);
        ConfigureRawArtifact(modelBuilder);

        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationArtifactMapConfiguration());
        modelBuilder.ApplyConfiguration(new ParsingFailureConfiguration());
    }

    private static void ConfigureImportBatch(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("ImportBatch");

            entity.HasKey(e => e.ImportBatchId);

            entity.Property(e => e.SourceSystem)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.SourceVersion)
                  .HasMaxLength(50);

            entity.Property(e => e.Status)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(e => e.ImportedBy)
                  .HasMaxLength(200);

            entity.Property(e => e.ImportLabel)
                  .HasMaxLength(200);

            entity.HasIndex(e => e.ImportedAtUtc)
                  .HasDatabaseName("IX_ImportBatch_ImportedAtUtc");
        });
    }

    private static void ConfigureRawArtifact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RawArtifact>(entity =>
        {
            entity.ToTable("RawArtifact");

            entity.HasKey(e => e.RawArtifactId);

            entity.Property(e => e.ArtifactType)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(e => e.ArtifactName)
                  .IsRequired()
                  .HasMaxLength(260);

            entity.Property(e => e.ContentType)
                  .HasMaxLength(100);

            entity.Property(e => e.Sha256)
                  .IsRequired()
                  .HasColumnType("binary(32)");

            entity.HasOne(e => e.ImportBatch)
                  .WithMany()
                  .HasForeignKey(e => e.ImportBatchId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ImportBatchId)
                  .HasDatabaseName("IX_RawArtifact_ImportBatchId");

            entity.HasIndex(e => e.Sha256)
                  .HasDatabaseName("IX_RawArtifact_Sha256");
        });
    }
}
