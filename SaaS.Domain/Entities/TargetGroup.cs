using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Domain.Entities
{
    public class TargetGroup
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int BotId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string GroupUrl { get; set; } = string.Empty;
        public string ConfigJson { get; set; } = "{}"; // UI Dynamic Config Parameters
        public string? LastCursor { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public User User { get; set; } = null!;
        public Bot Bot { get; set; } = null!;
        public ICollection<Scrape> Scrapes { get; set; } = new List<Scrape>();
        public ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}
