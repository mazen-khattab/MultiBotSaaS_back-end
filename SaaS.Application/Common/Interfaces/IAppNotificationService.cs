using System;
using System.Threading.Tasks;

namespace SaaS.Application.Common.Interfaces
{
    public interface IAppNotificationService
    {
        Task NotifyScrapeCompletedAsync(Guid userId, int scrapeId, int leadsCount);
        Task NotifyLeadStatusUpdatedAsync(Guid userId, long leadId, string status);
        Task NotifyJobFailedAsync(Guid userId, long jobId, string message);
    }
}
