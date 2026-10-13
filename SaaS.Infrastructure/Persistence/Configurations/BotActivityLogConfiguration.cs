using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class BotActivityLogConfiguration : IEntityTypeConfiguration<BotActivityLog>
{
    public void Configure(EntityTypeBuilder<BotActivityLog> builder)
    {
        builder.ToTable("BotActivityLogs");

        builder.HasKey(bal => bal.Id);

        builder.Property(bal => bal.Id)
            .UseIdentityColumn();

        builder.Property(bal => bal.CorrelationId)
            .IsRequired()
            .HasColumnType("varchar(100)");

        builder.Property(bal => bal.LogLevel)
            .IsRequired()
            .HasColumnType("varchar(20)")
            .HasDefaultValue("INFO");

        builder.Property(bal => bal.Message)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(bal => bal.StackTrace)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(bal => bal.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime")
            .HasDefaultValueSql("GETUTCDATE()");

        // Logs must survive even after the referenced User is deleted (or represent
        // a system/anonymous event with no user at all) - hence nullable FK + SetNull.
        builder.HasOne(bal => bal.User)
            .WithMany(u => u.BotActivityLogs)
            .HasForeignKey(bal => bal.UserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(bal => new { bal.CorrelationId, bal.UserId })
            .HasDatabaseName("IX_BotActivityLogs_CorrelationId_UserId");

        builder.HasOne(bal => bal.Bot)
            .WithMany(b => b.BotActivityLogs)
            .HasForeignKey(bal => bal.BotId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(bal => new { bal.UserId, bal.BotId, bal.CreatedAt, bal.Id });
    }
}
