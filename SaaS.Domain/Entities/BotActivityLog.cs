using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Domain.Entities
{
    public class BotActivityLog
    {
        public long Id { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        // Optional for older worker submissions that only identify a correlation.
        public int? BotId { get; set; }

        // LogLevel: "INFO", "WARN", "ERROR"
        public string LogLevel { get; set; } = "INFO";
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        public User? User { get; set; }
        public Bot? Bot { get; set; }
    }
}
