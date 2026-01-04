using ChatLake.Infrastructure.Gold.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatLake.Infrastructure.Persistence.Configurations.Gold;

public class UserOverrideConfiguration : IEntityTypeConfiguration<UserOverride>
{
    public void Configure(EntityTypeBuilder<UserOverride> builder)
    {
        builder.ToTable("UserOverride");

        builder.HasKey(e => e.UserOverrideId);

        builder.Property(e => e.OverrideType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.TargetType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(200);

        builder.HasIndex(e => new { e.TargetType, e.TargetId })
            .HasDatabaseName("IX_UserOverride_Target");

        builder.HasIndex(e => e.CreatedAtUtc)
            .HasDatabaseName("IX_UserOverride_CreatedAtUtc");
    }
}
