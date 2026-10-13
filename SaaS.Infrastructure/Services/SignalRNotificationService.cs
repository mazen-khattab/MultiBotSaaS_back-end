using Microsoft.AspNetCore.SignalR;
using SaaS.Application.Common.Interfaces;
using SaaS.Infrastructure.Hubs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Infrastructure.Services
{
    public class SignalRNotificationService : IAppNotificationService
    {
        private readonly IHubContext<AppNotificationHub> _hubContext;
        public SignalRNotificationService(IHubContext<AppNotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }
        public async Task NotifyScrapeCompletedAsync(Guid userId, int scrapeId, int leadsCount)
        {
            await _hubContext.Clients.User(userId.ToString())
                .SendAsync("ScrapeCompleted", new
                {
                    ScrapeId = scrapeId,
                    LeadsCount = leadsCount
                });
        }

        public async Task NotifyLeadStatusUpdatedAsync(Guid userId, long leadId, string status)
        {
            await _hubContext.Clients.User(userId.ToString())
                .SendAsync("LeadStatusUpdated", new
                {
                    LeadId = leadId,
                    Status = status
                });
        }

        public async Task NotifyJobFailedAsync(Guid userId, long jobId, string message)
        {
            await _hubContext.Clients.User(userId.ToString())
                .SendAsync("JobFailed", new
                {
                    JobId = jobId,
                    Message = message
                });
        }
    }
}
