using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class UserBotConfiguration : IEntityTypeConfiguration<UserBot>
{
    public void Configure(EntityTypeBuilder<UserBot> builder)
    {
        builder.ToTable("UserBots");

        builder.HasKey(ub => ub.Id);

        builder.Property(ub => ub.Id)
            .UseIdentityColumn();

        builder.Property(ub => ub.PurchasePrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(ub => ub.PurchaseDate)
            .IsRequired()
            .HasColumnType("datetime")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(ub => ub.User)
            .WithMany(u => u.UserBots)
            .HasForeignKey(ub => ub.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ub => ub.Bot)
            .WithMany(b => b.UserBots)
            .HasForeignKey(ub => ub.BotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ub => new { ub.UserId, ub.BotId })
            .IsUnique()
            .HasDatabaseName("IX_UserBots_UserId_BotId");
    }
}
