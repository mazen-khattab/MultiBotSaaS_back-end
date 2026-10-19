using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetEligible
{
    public class GetEligibleConnectedAccountsQueryHandler : IRequestHandler<GetEligibleConnectedAccountsQuery, ApiResponse<List<ConnectedAccountDto>>>
    {
        private readonly IAppDbContext _context;
        private readonly IUserBotService _userBotService;

        public GetEligibleConnectedAccountsQueryHandler(IAppDbContext context, IUserBotService userBotService)
        {
            _context = context;
            _userBotService = userBotService;
        }

        public async Task<ApiResponse<List<ConnectedAccountDto>>> Handle(GetEligibleConnectedAccountsQuery request, CancellationToken cancellationToken)
        { 
            var hasBot = await _userBotService.CheckOwnershipAsync(request.UserId, request.BotId, cancellationToken);
            if (!hasBot)
            {
                return ApiResponse<List<ConnectedAccountDto>>.Failure("User or bot not found", ErrorType.NotFound);
            }

            var activeDbString = AccountStatus.ACTIVE.ToDbString();
            var typeDbString = request.Type.ToDbString();

            var accounts = await _context.ConnectedAccounts
                .AsNoTracking()
                .Include(a => a.Cookie)
                .Where(a => a.UserId == request.UserId &&
                            a.BotId == request.BotId &&
                            a.Type == typeDbString &&
                            a.IsActive &&
                            a.Status == activeDbString &&
                            a.Cookie != null &&
                            a.Cookie.CookiesExpireDate > DateTime.UtcNow)
                .OrderBy(a => a.DisplayName)
                .ToListAsync(cancellationToken);

            var dtos = accounts.ToDtoList();

            if (dtos.Count == 0)
            {
                return ApiResponse<List<ConnectedAccountDto>>.Success(dtos, "No eligible active connected accounts found.");
            }

            return ApiResponse<List<ConnectedAccountDto>>.Success(dtos, "Eligible connected accounts retrieved successfully.");
        }
    }
}
