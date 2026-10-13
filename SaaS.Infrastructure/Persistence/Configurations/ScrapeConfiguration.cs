using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class ScrapeConfiguration : IEntityTypeConfiguration<Scrape>
{
    public void Configure(EntityTypeBuilder<Scrape> builder)
    {
        builder.ToTable("Scrapes");

        builder.HasKey(candidate => candidate.Id);

        builder.Property(candidate => candidate.Id)
            .UseIdentityColumn();

        builder.Property(candidate => candidate.CollectedLeadsCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(candidate => candidate.InfoJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}");

        builder.Property(candidate => candidate.StartedAt)
            .IsRequired()
            .HasColumnType("datetime")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(candidate => candidate.EndedAt)
            .HasColumnType("datetime")
            .IsRequired(false);

        builder.Property(candidate => candidate.Status)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasDefaultValue("Running");

        // User is required and Restrict-deleted: a Scrape must always belong to a user,
        // and that user cannot be hard-deleted while scrapes still reference them.
        builder.HasOne(candidate => candidate.User)
            .WithMany(u => u.Scrapes)
            .HasForeignKey(candidate => candidate.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bot / TargetGroup / ConnectedAccount are optional (nullable FK) and use
        // SetNull on delete: a Scrape is historical data, so if the Bot/Group/Account it
        // used is later removed, the Scrape survives with that reference cleared instead
        // of being deleted itself or blocking the parent's deletion.
        builder.HasOne(candidate => candidate.Bot)
            .WithMany(b => b.Scrapes)
            .HasForeignKey(candidate => candidate.BotId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(candidate => candidate.Group)
            .WithMany(g => g.Scrapes)
            .HasForeignKey(candidate => candidate.GroupId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(candidate => candidate.Account)
            .WithMany(a => a.Scrapes)
            .HasForeignKey(candidate => candidate.AccountId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Covers the dashboard-style query: "this user's scrapes, for this bot, filtered
        // by status" (e.g. list all 'Running' scrapes for User X on Bot Y).
        builder.HasIndex(candidate => new { candidate.UserId, candidate.BotId, candidate.Status })
            .HasDatabaseName("IX_Scrape_UserId_BotId_Status");
    }
}
