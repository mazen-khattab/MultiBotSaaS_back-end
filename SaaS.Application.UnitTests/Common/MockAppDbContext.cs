using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace SaaS.Application.UnitTests.Common
{
    public class MockAppDbContext : DbContext, IAppDbContext
    {
        public MockAppDbContext(DbContextOptions<MockAppDbContext> options) : base(options)
        {
        }

        public DbSet<ConnectedAccount> ConnectedAccounts { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Bot> Bots { get; set; } = null!;
        public DbSet<Scrape> Scrapes { get; set; } = null!;
        public DbSet<Lead> Leads { get; set; } = null!;
        public DbSet<TargetGroup> TargetGroups { get; set; } = null!;
        public DbSet<UserSetting> UserSettings { get; set; } = null!;
        public DbSet<Job> Jobs { get; set; } = null!;
        public DbSet<BotActivityLog> BotActivityLogs { get; set; } = null!;

        public DbSet<SystemAdmin> SystmeAdmins { get; set; } = null!;
        public DbSet<UserBot> UserBots { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<ConnectedAccountCookie> ConnectedAccountCookies { get; set; } = null!;
        public DbSet<LeadDetail> LeadDetails { get; set; } = null!;
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;
        public DbSet<SystemAdminRefreshTokens> SystemAdminRefreshTokens { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConnectedAccountCookie>().HasKey(c => c.AccountId);
            modelBuilder.Entity<UserBot>().HasKey(ub => new { ub.UserId, ub.BotId });
            modelBuilder.Entity<LeadDetail>().HasKey(ld => ld.LeadId);
            
            base.OnModelCreating(modelBuilder);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
