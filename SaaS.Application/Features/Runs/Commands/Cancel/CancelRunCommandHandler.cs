using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Application.Features.Runs.Commands.Cancel
{
    public class CancelRunCommandHandler : IRequestHandler<CancelRunCommand, ApiResponse<bool>>
    {
        private readonly IAppDbContext _context;

        public CancelRunCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<bool>> Handle(CancelRunCommand request, CancellationToken cancellationToken)
        {
            var run = await _context.Runs
                .FirstOrDefaultAsync(r => r.Id == request.RunId && r.UserId == request.UserId, cancellationToken);

            if (run == null)
            {
                return ApiResponse<bool>.Failure("Run not found.", ErrorType.NotFound);
            }

            if (!string.Equals(run.Status, RunStatus.RUNNING.ToDbString(), StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<bool>.Failure($"Cannot cancel run with status '{run.Status}'. Only running runs can be cancelled.", ErrorType.ValidationError);
            }

            run.Status = RunStatus.CANCELLED.ToDbString();
            run.EndedAt = DateTime.UtcNow;

            if (run.AccountId.HasValue)
            {
                var account = await _context.ConnectedAccounts.FirstOrDefaultAsync(a => a.Id == run.AccountId.Value, cancellationToken);
                if (account != null && string.Equals(account.Status, AccountStatus.BUSY.ToDbString(), StringComparison.OrdinalIgnoreCase))
                {
                    account.Status = AccountStatus.ACTIVE.ToDbString();
                    account.LastStatusUpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, "Run cancelled successfully.");
        }
    }
}
