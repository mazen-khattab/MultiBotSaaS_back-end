using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Interfaces
{
    public interface IAppDbContext
    {
        DatabaseFacade Database { get; }
        DbSet<SystemAdmin> SystmeAdmins { get; }
        DbSet<User> Users { get; }
        DbSet<Bot> Bots { get; }
        DbSet<UserSetting> UserSettings { get; }
        DbSet<UserBot> UserBots { get; }
        DbSet<Transaction> Transactions { get; }
        DbSet<ConnectedAccount> ConnectedAccounts { get; }
        DbSet<ConnectedAccountCookie> ConnectedAccountCookies { get; }
        DbSet<TargetGroup> TargetGroups { get; }
        DbSet<Scrape> Scrapes { get; }
        DbSet<Job> Jobs { get; }
        DbSet<Lead> Leads { get; }
        DbSet<LeadDetail> LeadDetails { get; }
        DbSet<BotActivityLog> BotActivityLogs { get; }
        DbSet<UserRefreshToken> UserRefreshTokens { get; }
        DbSet<SystemAdminRefreshTokens> SystemAdminRefreshTokens { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
