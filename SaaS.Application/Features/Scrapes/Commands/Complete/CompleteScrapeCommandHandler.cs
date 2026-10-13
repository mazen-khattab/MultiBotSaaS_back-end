using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Application.Features.Scrapes.Commands.Complete
{
    public class CompleteScrapeCommandHandler : IRequestHandler<CompleteScrapeCommand, ApiResponse<bool>>
    {
        private readonly IAppDbContext _context;
        private readonly IAppNotificationService _notificationService;
        private readonly ILogger<CompleteScrapeCommandHandler> _logger;

        public CompleteScrapeCommandHandler(
            IAppDbContext context, 
            IAppNotificationService notificationService,
            ILogger<CompleteScrapeCommandHandler> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ApiResponse<bool>> Handle(CompleteScrapeCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting completion process for ScrapeId {ScrapeId} with {LeadCount} incoming leads", request.ScrapeId, request.Leads?.Count ?? 0);

            _logger.LogDebug("Fetching scrape from database for ScrapeId: {ScrapeId}", request.ScrapeId);
            var scrape = await _context.Scrapes
                .FirstOrDefaultAsync(candidate => candidate.Id == request.ScrapeId, cancellationToken);

            if (scrape is null)
            {
                _logger.LogWarning("Scrape not found for ScrapeId: {ScrapeId}", request.ScrapeId);
                return ApiResponse<bool>.Failure("Scrape not found.", ErrorType.NotFound);
            }

            if (!string.Equals(scrape.Status, ScrapeStatus.RUNNING.ToDbString(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Cannot complete ScrapeId {ScrapeId}. Current status is '{Status}', expected '{ExpectedStatus}'", scrape.Id, scrape.Status, ScrapeStatus.RUNNING.ToDbString());
                return ApiResponse<bool>.Failure("Scrape is not in progress or has already been finalized.", ErrorType.ValidationError);
            }

            var newLeads = new List<Lead>();

            // Process leads only if there are any
            if (request.Leads.Any())
            {
                var incomingIds = request.Leads
                    .Select(x => x.ExternalId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                if (incomingIds.Any())
                {
                    _logger.LogDebug("Checking database for {IncomingCount} incoming lead IDs for UserId: {UserId}", incomingIds.Count, scrape.UserId);
                    var existingFromDb = await _context.Leads
                        .AsNoTracking()
                        .Where(l => l.UserId == scrape.UserId && incomingIds.Contains(l.ExternalId))
                        .Select(l => l.ExternalId)
                        .ToListAsync(cancellationToken);

                    _logger.LogDebug("Found {ExistingCount} existing leads out of {IncomingCount} incoming IDs", existingFromDb.Count, incomingIds.Count);
                    var existingIds = new HashSet<string>(existingFromDb, StringComparer.OrdinalIgnoreCase);

                    var newUniqueLeadsDto = request.Leads
                        .Where(l => !string.IsNullOrWhiteSpace(l.ExternalId) && !existingIds.Contains(l.ExternalId));

                    newLeads = newUniqueLeadsDto.Select(dto => new Lead
                    {
                        UserId = scrape.UserId,
                        BotId = scrape.BotId,
                        ExternalId = dto.ExternalId!,
                        GroupId = scrape.GroupId,
                        AccountId = scrape.AccountId,
                        ScrapeId = scrape.Id,
                        ProfileName = dto.Username ?? string.Empty,
                        ProfileUrl = dto.ExternalId!,
                        AiMessage = dto.AiMessage ?? string.Empty,
                        Status = LeadStatus.PENDING.ToDbString(),
                        CreatedAt = DateTime.UtcNow,
                        Detail = new LeadDetail { MetaDataJson = string.IsNullOrWhiteSpace(dto.MetadataJson) ? "{}" : dto.MetadataJson }
                    }).ToList();

                    if (newLeads.Any())
                    {
                        _logger.LogInformation("Persisting {NewLeadsCount} new unique leads for ScrapeId {ScrapeId}", newLeads.Count, scrape.Id);
                        await _context.Leads.AddRangeAsync(newLeads, cancellationToken);
                    }
                    else
                    {
                        _logger.LogInformation("All incoming leads were duplicates. No new leads to persist for ScrapeId {ScrapeId}", scrape.Id);
                    }
                }
                else
                {
                    _logger.LogDebug("No valid ExternalIds found in incoming leads for ScrapeId {ScrapeId}", scrape.Id);
                }
            }
            else
            {
                _logger.LogInformation("No incoming leads provided for ScrapeId {ScrapeId}", scrape.Id);
            }

            // Single Path: Finalize scrape
            _logger.LogDebug("Updating scrape status to COMPLETED and saving changes for ScrapeId: {ScrapeId}", scrape.Id);
            scrape.Status = ScrapeStatus.COMPLETED.ToDbString();
            scrape.CollectedLeadsCount = newLeads.Count;
            scrape.EndedAt = DateTime.UtcNow;

            if (scrape.AccountId.HasValue)
            {
                var account = await _context.ConnectedAccounts.FirstOrDefaultAsync(a => a.Id == scrape.AccountId, cancellationToken);
                if (account != null && account.Status == AccountStatus.BUSY.ToDbString())
                {
                    account.Status = AccountStatus.ACTIVE.ToDbString();
                    account.LastStatusUpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Scrape {ScrapeId} successfully marked as completed with {CollectedLeadsCount} new leads", scrape.Id, scrape.CollectedLeadsCount);

            // Single Path: Notify
            try
            {
                _logger.LogDebug("Sending completion notification for ScrapeId: {ScrapeId}", scrape.Id);
                await _notificationService.NotifyScrapeCompletedAsync(scrape.UserId, scrape.Id, newLeads.Count);
                _logger.LogInformation("Successfully sent completion notification for ScrapeId: {ScrapeId}", scrape.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send scrape completion notification for ScrapeId {ScrapeId}", scrape.Id);
            }

            return ApiResponse<bool>.Success(true, 
                newLeads.Any() ? "Scrape completed and unique leads persisted." : "Scrape completed with no leads.");
        }
    }
}
