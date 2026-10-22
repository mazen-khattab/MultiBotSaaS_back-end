using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaaS.Domain.Entities;
using SaaS.Infrastructure.Persistence;

namespace SaaS.Api.IntegrationTests;

public sealed class LeadDetailsPersistenceTests
{
    [Fact]
    public async Task MetadataIsStoredInSeparateTableWithTheGeneratedLeadId()
    {
        await using var db = await CreateDatabase();
        const string metadata = "{\"bio\":\"Consultant\",\"comments\":[\"Interested\"]}";
        var lead = new Lead { User = new User(), Detail = new LeadDetail { MetaDataJson = metadata } };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Equal(metadata, (await db.LeadDetails.SingleAsync(detail => detail.LeadId == lead.Id)).MetaDataJson);
        Assert.Equal(metadata, (await db.Leads.Include(candidate => candidate.Detail).SingleAsync()).Detail!.MetaDataJson);

        // Check the physical schema so sharing the Leads table cannot silently return.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Leads') WHERE name = 'MetaDataJson';";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('LeadDetails') WHERE name = 'MetaDataJson';";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task DetailsCanBeAddedLaterAndCannotBeDuplicatedOrOrphaned()
    {
        await using var db = await CreateDatabase();
        var lead = new Lead { User = new User() };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Null((await db.Leads.Include(candidate => candidate.Detail).SingleAsync()).Detail);
        Assert.Empty(await db.LeadDetails.ToListAsync());

        db.LeadDetails.Add(new LeadDetail { LeadId = lead.Id });
        await db.SaveChangesAsync();
        Assert.Equal("{}", (await db.LeadDetails.SingleAsync()).MetaDataJson);

        var duplicate = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO LeadDetails (LeadId, MetaDataJson) VALUES ({lead.Id}, '{{}}')"));
        Assert.Equal(1555, duplicate.SqliteExtendedErrorCode); // SQLITE_CONSTRAINT_PRIMARYKEY
        var orphan = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO LeadDetails (LeadId, MetaDataJson) VALUES ({long.MaxValue}, '{{}}')"));
        Assert.Equal(787, orphan.SqliteExtendedErrorCode); // SQLITE_CONSTRAINT_FOREIGNKEY
    }

    [Fact]
    public async Task DeletingLeadDeletesItsDetailsWithoutLoadingThem()
    {
        await using var db = await CreateDatabase();
        db.Leads.Add(new Lead { User = new User(), Detail = new LeadDetail() });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // ExecuteDelete bypasses EF's tracked cascade handling and exercises the database FK.
        Assert.Equal(1, await db.Leads.ExecuteDeleteAsync());
        Assert.Empty(await db.LeadDetails.ToListAsync());
        Assert.Single(await db.Users.ToListAsync());
    }

    private static async Task<AppDbContext> CreateDatabase()
    {
        var db = new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:;Foreign Keys=True").Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
