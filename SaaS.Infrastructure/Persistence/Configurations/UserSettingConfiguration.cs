using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        builder.ToTable("UserSettings");

        builder.HasKey(us => us.Id);

        builder.Property(us => us.Id)
            .UseIdentityColumn();

        // Enforces the 1-to-1 cardinality at the database level: only one
        // UserSetting row can ever exist per UserId. Without this unique index,
        // nothing stops a second row sharing the same UserId and the relationship
        // silently degrading into 1-to-many.
        builder.HasIndex(us => us.UserId)
            .IsUnique()
            .HasDatabaseName("IX_UserSettings_UserId");

        builder.Property(us => us.CompanyName)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(us => us.CompanyPitch)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(us => us.DailyMessageLimit)
            .IsRequired()
            // Zero is a valid user setting and must be inserted explicitly.
            // Use an invalid limit as EF's unset marker, preserving the database default.
            .HasSentinel(-1)
            .HasDefaultValue(50);

        builder.Property(us => us.AIApiKeyEncrypted)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(us => us.ScraperApiTokenEncrypted)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(us => us.AIApiKeyExpirationDate)
            .IsRequired()
            .HasDefaultValueSql("DATEADD(day, 7, GETUTCDATE())");

        builder.Property(us => us.ScraperApiTokenExpirationDate)
            .IsRequired()
            .HasDefaultValueSql("DATEADD(day, 7, GETUTCDATE())");

        // The actual FK + Cascade delete behavior for this relationship is configured
        // once, on the UserConfiguration side, via HasForeignKey<UserSetting>(us => us.UserId).
    }
}
