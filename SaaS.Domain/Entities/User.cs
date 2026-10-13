using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // Single Active Session strategy
        public string? CurrentSessionToken { get; set; }
        public string? LastLoginIp { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public UserSetting Setting { get; set; } = null!;
        public ICollection<UserBot> UserBots { get; set; } = new List<UserBot>();
        public ICollection<ConnectedAccount> ConnectedAccounts { get; set; } = new List<ConnectedAccount>();
        public ICollection<TargetGroup> TargetGroups { get; set; } = new List<TargetGroup>();
        public ICollection<Scrape> Scrapes { get; set; } = new List<Scrape>();
        public ICollection<Lead> Leads { get; set; } = new List<Lead>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<UserRefreshToken> RefreshTokens { get; set; } = new List<UserRefreshToken>();
        public ICollection<BotActivityLog> BotActivityLogs { get; set; } = new List<BotActivityLog>();
        public ICollection<Job> Jobs { get; set; } = new List<Job>();

    }
}
