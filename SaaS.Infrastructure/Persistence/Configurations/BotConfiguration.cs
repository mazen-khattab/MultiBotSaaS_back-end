using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class BotConfiguration : IEntityTypeConfiguration<Bot>
{
    public void Configure(EntityTypeBuilder<Bot> builder)
    {
        builder.ToTable("Bots");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .UseIdentityColumn();

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Description)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(b => b.CurrentPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(b => b.UiModuleCode)
            .IsRequired()
            .HasColumnType("varchar(50)");

        builder.Property(b => b.CooldownMinutes)
            .IsRequired()
            .HasDefaultValue(180);

        builder.Property(b => b.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // All 1-to-many relationships from Bot (UserBot, ConnectedAccount, TargetGroup,
        // Scrape, Lead) are configured in each dependent entity's own configuration file.
    }
}