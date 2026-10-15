using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Application.Features.Scrapes.Commands.Cancel
{
    public class CancelScrapeCommandHandler : IRequestHandler<CancelScrapeCommand, ApiResponse<bool>>
    {
        private readonly IAppDbContext _context;
        private readonly IUserBotService _userBotService;   

        public CancelScrapeCommandHandler(IAppDbContext context, IUserBotService userBotService)
        {
            _context = context;
            _userBotService = userBotService;
        }

        public async Task<ApiResponse<bool>> Handle(CancelScrapeCommand request, CancellationToken cancellationToken)
        {
            var hasOwnership = await _userBotService.CheckOwnershipAsync(request.UserId, request.ScrapeId, cancellationToken);
            if (!hasOwnership)
            {
                return ApiResponse<bool>.Failure("User or bot not found.", ErrorType.Unauthorized);
            }

            var scrape = await _context.Scrapes
                .FirstOrDefaultAsync(candidate => candidate.Id == request.ScrapeId && candidate.UserId == request.UserId, cancellationToken);

            if (scrape == null)
            {
                return ApiResponse<bool>.Failure("Scrape not found.", ErrorType.NotFound);
            }

            if (!string.Equals(scrape.Status, ScrapeStatus.RUNNING.ToDbString(), StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<bool>.Failure($"Cannot cancel scrape with status '{scrape.Status}'. Only running scrapes can be cancelled.", ErrorType.ValidationError);
            }

            scrape.Status = ScrapeStatus.CANCELLED.ToDbString();
            scrape.EndedAt = DateTime.UtcNow;

            if (scrape.AccountId.HasValue)
            {
                var account = await _context.ConnectedAccounts.FirstOrDefaultAsync(a => a.Id == scrape.AccountId.Value &&
                    a.UserId == request.UserId && a.BotId == scrape.BotId, cancellationToken);
                if (account != null && string.Equals(account.Status, AccountStatus.BUSY.ToDbString(), StringComparison.OrdinalIgnoreCase))
                {
                    account.Status = AccountStatus.ACTIVE.ToDbString();
                    account.LastStatusUpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, "Scrape cancelled successfully.");
        }
    }
}
