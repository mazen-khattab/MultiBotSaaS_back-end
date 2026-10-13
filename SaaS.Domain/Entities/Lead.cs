using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Domain.Entities
{
    public class Lead
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public int? BotId { get; set; }
        public int? GroupId { get; set; }
        public int? AccountId { get; set; }
        public int? ScrapeId { get; set; }

        public string ProfileName { get; set; } = string.Empty;
        public string ProfileUrl { get; set; } = string.Empty;
        public string? AiMessage { get; set; }

        // Status: "Pending", "Completed", "Failed"
        public string Status { get; set; } = LeadStatus.PENDING.ToDbString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }

        // Navigation Properties
        public User User { get; set; } = null!;
        public Bot? Bot { get; set; }
        public TargetGroup? Group { get; set; }
        public ConnectedAccount? Account { get; set; }
        public Scrape? Scrape { get; set; }

        // 1-to-1 Heavy Metadata Partitioning
        public LeadDetail? Detail { get; set; }
    }
}
