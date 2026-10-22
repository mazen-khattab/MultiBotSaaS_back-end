using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaaS.Domain.Entities;

namespace SaaS.Infrastructure.Persistence.Configurations;

public class LeadDetailConfiguration : IEntityTypeConfiguration<LeadDetail>
{
    public void Configure(EntityTypeBuilder<LeadDetail> builder)
    {
        // Keep heavy metadata in its own table, linked by the lead's primary key.
        builder.ToTable("LeadDetails");

        builder.HasKey(ld => ld.LeadId);

        builder.Property(ld => ld.LeadId)
            .ValueGeneratedNever();

        builder.Property(ld => ld.MetaDataJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}");
    }
}
