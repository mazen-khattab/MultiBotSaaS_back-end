using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class ConnectedAccountConfiguration : IEntityTypeConfiguration<ConnectedAccount>
{
    public void Configure(EntityTypeBuilder<ConnectedAccount> builder)
    {
        builder.ToTable("ConnectedAccounts");

        builder.HasKey(ca => ca.Id);

        builder.Property(ca => ca.Id)
            .UseIdentityColumn();

        builder.Property(ca => ca.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Platform)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ca => ca.Type)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(AccountType.SCRAPING.ToDbString());

        //builder.Property(ca => ca.CookiesEncrypted)
        //    .IsRequired()
        //    .HasColumnType("nvarchar(max)");

        //builder.Property(ca => ca.CookiesExpireDate)
        //    .IsRequired()
        //    .HasColumnType("datetime");

        builder.Property(ca => ca.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(ca => ca.LastStatusUpdatedAt)
            .IsRequired();

        builder.HasOne(a => a.Cookie)
            .WithOne(t => t.Account)
            .HasForeignKey<ConnectedAccountCookie>(t => t.AccountId);

        builder.HasOne(ca => ca.User)
            .WithMany(u => u.ConnectedAccounts)
            .HasForeignKey(ca => ca.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ca => ca.Bot)
            .WithMany(b => b.ConnectedAccounts)
            .HasForeignKey(ca => ca.BotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite index for the common lookup pattern: "this user's connected
        // accounts for this specific bot". A single-column index on UserId alone
        // would not satisfy the BotId filter without an extra key lookup.
        builder.HasIndex(ca => new { ca.UserId, ca.BotId })
            .HasDatabaseName("IX_ConnectedAccounts_UserId_BotId");

        builder.HasIndex(ca => new { ca.UserId, ca.Status })
            .HasDatabaseName("IX_ConnectedAccounts_UserId_Status");
    }
}
