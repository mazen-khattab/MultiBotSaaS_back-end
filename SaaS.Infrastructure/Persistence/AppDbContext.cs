using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SaaS.Infrastructure.Persistence
{
    public class AppDbContext : DbContext, IAppDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<SystemAdmin> SystmeAdmins => Set<SystemAdmin>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Bot> Bots => Set<Bot>();
        public DbSet<UserSetting> UserSettings => Set<UserSetting>();
        public DbSet<UserBot> UserBots => Set<UserBot>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
        public DbSet<ConnectedAccountCookie> ConnectedAccountCookies => Set<ConnectedAccountCookie>();
        public DbSet<TargetGroup> TargetGroups => Set<TargetGroup>();
        public DbSet<Scrape> Scrapes => Set<Scrape>();
        public DbSet<Job> Jobs => Set<Job>();
        public DbSet<Lead> Leads => Set<Lead>();
        public DbSet<LeadDetail> LeadDetails => Set<LeadDetail>();
        public DbSet<BotActivityLog> BotActivityLogs => Set<BotActivityLog>();
        public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();
        public DbSet<SystemAdminRefreshTokens> SystemAdminRefreshTokens => Set<SystemAdminRefreshTokens>();


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
