using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Domain.Entities
{
    public class Scrape
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int? BotId { get; set; }
        public int? GroupId { get; set; }
        public int? AccountId { get; set; }

        public int CollectedLeadsCount { get; set; } = 0;
        public string InfoJson { get; set; } = "{}";
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }

        // State: "Running", "Completed", "Failed"
        public string Status { get; set; } = "Running";

        // Navigation Properties
        public User User { get; set; } = null!;
        public Bot? Bot { get; set; }
        public TargetGroup? Group { get; set; }
        public ConnectedAccount? Account { get; set; }

        //public ICollection<Job> Jobs { get; set; } = new List<Job>();
        public ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}
