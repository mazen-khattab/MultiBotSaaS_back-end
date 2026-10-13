using Microsoft.EntityFrameworkCore;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Api.IntegrationTests;

internal sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Preserve defaults, constraints and relationships while adapting SQL Server syntax.
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            if (property.GetColumnType()?.Contains("(max)", StringComparison.OrdinalIgnoreCase) == true)
                property.SetColumnType("TEXT");
            if (property.GetDefaultValueSql() == "GETUTCDATE()")
                property.SetDefaultValueSql("CURRENT_TIMESTAMP");
            if (property.GetDefaultValueSql() == "DATEADD(day, 7, GETUTCDATE())")
                property.SetDefaultValueSql("datetime(CURRENT_TIMESTAMP, '+7 days')");
        }
    }
}
