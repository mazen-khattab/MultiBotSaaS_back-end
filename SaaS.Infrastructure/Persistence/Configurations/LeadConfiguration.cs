using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .UseIdentityColumn();

        builder.Property(l => l.ExternalId)
            .IsRequired();

        builder.Property(l => l.ProfileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(l => l.ProfileUrl)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(l => l.AiMessage)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(l => l.Status)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasDefaultValue("Pending");

        builder.Property(l => l.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime")
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(l => l.User)
            .WithMany(u => u.Leads)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Scrape)
            .WithMany(candidate => candidate.Leads)
            .HasForeignKey(l => l.ScrapeId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(l => l.Bot)
            .WithMany(b => b.Leads)
            .HasForeignKey(l => l.BotId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(l => l.Group)
            .WithMany(g => g.Leads)
            .HasForeignKey(l => l.GroupId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(l => l.Account)
            .WithMany(a => a.Leads)
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // LeadDetail shares the lead's key in a separate table and is deleted with it.
        builder.HasOne(l => l.Detail)
            .WithOne(ld => ld.Lead)
            .HasForeignKey<LeadDetail>(ld => ld.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.ProfileUrl)
            .HasDatabaseName("IX_Leads_ProfileUrl");

        builder.HasIndex(l => new { l.UserId, l.ExternalId })
            .HasDatabaseName("IX_Leads_UserId_ExternalId");

        // Covers the most common Lead query pattern: "this user's leads, from this
        // scrape, filtered by status" (e.g. pull all 'Pending' leads for a given scrape).
        builder.HasIndex(l => new { l.UserId, l.ScrapeId, l.Status })
            .HasDatabaseName("IX_Leads_UserId_ScrapeId_Status");
    }
}
