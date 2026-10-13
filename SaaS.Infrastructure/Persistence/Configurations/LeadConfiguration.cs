using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        // Table splitting: Lead and LeadDetail are two entity classes mapped onto the
        // SAME physical table ("Leads"). Both configuration classes must call ToTable
        // with the identical name, and they share one primary key value
        // (LeadDetail.LeadId == Lead.Id) so that both map to the same row.
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

        // This is what actually makes the table split work: HasForeignKey<LeadDetail>
        // tells EF that LeadDetail.LeadId is simultaneously LeadDetail's own primary
        // key AND the value tying it to Lead.Id in the same row of the same table.
        builder.HasOne(l => l.Detail)
            .WithOne(ld => ld.Lead)
            .HasForeignKey<LeadDetail>(ld => ld.LeadId);

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