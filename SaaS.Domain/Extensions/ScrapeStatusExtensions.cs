using SaaS.Domain.Enums;
using System;

namespace SaaS.Domain.Extensions
{
    public static class ScrapeStatusExtensions
    {
        public static string ToDbString(this ScrapeStatus status) => status switch
        {
            ScrapeStatus.RUNNING => "Running",
            ScrapeStatus.PENDING => "Pending",
            ScrapeStatus.COMPLETED => "Completed",
            ScrapeStatus.FAILED => "Failed",
            ScrapeStatus.CANCELLED => "Cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ScrapeStatus value.")
        };

        public static ScrapeStatus ParseFromDbToScrapeStatus(this string dbValue) => dbValue switch
        {
            "Running" => ScrapeStatus.RUNNING,
            "Pending" => ScrapeStatus.PENDING,
            "Completed" => ScrapeStatus.COMPLETED,
            "Failed" => ScrapeStatus.FAILED,
            "Cancelled" => ScrapeStatus.CANCELLED,
            _ => throw new ArgumentOutOfRangeException(nameof(dbValue), dbValue, "Unrecognized ScrapeStatus value from DB.")
        };
    }
}
