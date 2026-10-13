using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Domain.Entities
{
    public class Bot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal CurrentPrice { get; set; }
        public string UiModuleCode { get; set; } = string.Empty;

        // Anti-Spam & Rate Limiting (Default 3 Hours = 180 Mins)
        public int CooldownMinutes { get; set; } = 180;
        public bool IsActive { get; set; } = true;
        public string Status { get; set; } = AccountStatus.ACTIVE.ToDbString();

        // Navigation Properties
        public ICollection<UserBot> UserBots { get; set; } = new List<UserBot>();
        public ICollection<ConnectedAccount> ConnectedAccounts { get; set; } = new List<ConnectedAccount>();
        public ICollection<TargetGroup> TargetGroups { get; set; } = new List<TargetGroup>();
        public ICollection<Scrape> Scrapes { get; set; } = new List<Scrape>();
        public ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}
