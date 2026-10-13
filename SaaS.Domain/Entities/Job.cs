using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SaaS.Domain.Entities
{
    public class Job
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public int? BotId { get; set; }
        //public int ScrapeId { get; set; }
        public string Type { get; set; } = string.Empty; // e.g. "ScrapeGroup", "SendDm"
        public string PayloadJson { get; set; } = "{}";

        // Status: "Pending", "Processing", "Completed", "Failed"
        public string Status { get; set; } = JobStatus.PENDING.ToDbString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // Navigation Properties
        //public Scrape Scrape { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
