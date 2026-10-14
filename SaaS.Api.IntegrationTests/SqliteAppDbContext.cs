using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Api.IntegrationTests;

internal sealed class SqliteAppDbContext : AppDbContext
{
    public SqliteAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        // Model a case-insensitive SQL Server database for profile/module substring searches.
        // SQLite translates Contains to instr, which otherwise ignores column collations.
        var connection = (SqliteConnection)Database.GetDbConnection();
        connection.CreateFunction<string?, string?, int?>("instr", (value, search) =>
            value is null || search is null ? null : value.IndexOf(search, StringComparison.OrdinalIgnoreCase) + 1,
            isDeterministic: true);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Preserve defaults, constraints and relationships while adapting SQL Server syntax.
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            // Security-token columns retain SQLite's case-sensitive comparison behavior.
            if (property.ClrType == typeof(string) && property.Name is "Email" or "FullName" or "Name" or "UiModuleCode")
                property.SetCollation("NOCASE");
            if (property.GetColumnType()?.Contains("(max)", StringComparison.OrdinalIgnoreCase) == true)
                property.SetColumnType("TEXT");
            if (property.GetDefaultValueSql() == "GETUTCDATE()")
                property.SetDefaultValueSql("CURRENT_TIMESTAMP");
            if (property.GetDefaultValueSql() == "DATEADD(day, 7, GETUTCDATE())")
                property.SetDefaultValueSql("datetime(CURRENT_TIMESTAMP, '+7 days')");
        }
    }
}
