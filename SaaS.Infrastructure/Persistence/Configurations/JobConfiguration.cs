using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .UseIdentityColumn();

        builder.Property(j => j.Type)
            .IsRequired()
            .HasColumnType("varchar(100)");

        builder.Property(j => j.UserId)
            .IsRequired();

        // BotId is a plain nullable column, not a navigated relationship: the spec
        // gives it no "FK -> Bot" designation (unlike ScrapeId below), so it's treated
        // as a denormalized reference for fast filtering/reporting only.
        builder.Property(j => j.BotId)
            .IsRequired(false);

        builder.Property(j => j.PayloadJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(j => j.Status)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasDefaultValue("Pending");

        builder.Property(j => j.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime")
            .HasDefaultValueSql("GETUTCDATE()");

        // Cascade here is intentional and safe: Jobs are queue entries owned entirely
        // by a Scrape and have no meaning once that Scrape is gone. Only one path reaches
        // Job through Scrape, so this doesn't create the multiple-cascade-path problem
        // (SQL Server Error 1750) that the Restrict/SetNull choices elsewhere avoid.
        //builder.HasOne(j => j.Scrape)
        //    .WithMany(r => r.Jobs)
        //    .HasForeignKey(j => j.ScrapeId)
        //    .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.User)
            .WithMany(u => u.Jobs)
            .HasForeignKey(j => j.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => j.Status)
            .HasDatabaseName("IX_Jobs_Status");

        //builder.HasIndex(j => j.ScrapeId)
        //    .HasDatabaseName("IX_Jobs_ScrapeId");
    }
}