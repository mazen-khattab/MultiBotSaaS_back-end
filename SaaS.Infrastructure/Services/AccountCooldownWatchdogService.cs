using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaaS.Application.Features.ConnectedAccounts.Commands.ProcessCooldown;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SaaS.Infrastructure.Services
{
    public class AccountCooldownWatchdogService : BackgroundService
    {
        private readonly ILogger<AccountCooldownWatchdogService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public AccountCooldownWatchdogService(
            ILogger<AccountCooldownWatchdogService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Check account cooldowns every 15 minutes.
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _logger.LogDebug("Starting 15-minute account cooldown check.");

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(new ProcessAccountCooldownCommand(), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing account cooldown check.");
                }
            }
        }
    }
}
