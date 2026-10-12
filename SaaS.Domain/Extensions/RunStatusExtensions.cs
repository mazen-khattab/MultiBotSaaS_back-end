using SaaS.Domain.Enums;
using System;

namespace SaaS.Domain.Extensions
{
    public static class RunStatusExtensions
    {
        public static string ToDbString(this RunStatus status) => status switch
        {
            RunStatus.RUNNING => "Running",
            RunStatus.PENDING => "Pending",
            RunStatus.COMPLETED => "Completed",
            RunStatus.FAILED => "Failed",
            RunStatus.CANCELLED => "Cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped RunStatus value.")
        };

        public static RunStatus ParseFromDbToRunStatus(this string dbValue) => dbValue switch
        {
            "Running" => RunStatus.RUNNING,
            "Pending" => RunStatus.PENDING,
            "Completed" => RunStatus.COMPLETED,
            "Failed" => RunStatus.FAILED,
            "Cancelled" => RunStatus.CANCELLED,
            _ => throw new ArgumentOutOfRangeException(nameof(dbValue), dbValue, "Unrecognized RunStatus value from DB.")
        };
    }
}
