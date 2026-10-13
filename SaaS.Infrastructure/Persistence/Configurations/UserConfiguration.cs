using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(u => u.CurrentSessionToken)
            .HasMaxLength(450)
            .IsRequired(false);

        builder.Property(u => u.LastLoginIp)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(u => u.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime")
            .HasDefaultValueSql("GETUTCDATE()");

        // 1-to-1 with UserSetting. Configured here (the principal side) because this
        // is the single source of truth for the relationship; UserSetting's own config
        // only declares column shapes and the unique index, not the FK/cascade itself.
        builder.HasOne(u => u.Setting)
            .WithOne(us => us.User)
            .HasForeignKey<UserSetting>(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The 1-to-many relationships to UserBot, ConnectedAccount, TargetGroup, Scrape,
        // Lead and Transaction are deliberately configured in each dependent entity's
        // own configuration file instead of here, so each relationship has exactly one
        // place where its FK/DeleteBehavior is defined.
    }
}