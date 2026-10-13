using System;
using System.Collections.Generic;
using System.Text;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Domain.Entities
{
    public class ConnectedAccount
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int BotId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        
        // Status: "Active", "Busy", "CoolingDown", "AccountFlagged", "Banned"
        public string Status { get; set; } = AccountStatus.ACTIVE.ToDbString();
        public DateTime LastStatusUpdatedAt { get; set; }

        // Navigation Properties
        public ConnectedAccountCookie Cookie { get; set; } = null!;
        public User User { get; set; } = null!;
        public Bot Bot { get; set; } = null!;
        public ICollection<Scrape> Scrapes { get; set; } = new List<Scrape>();
        public ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}
