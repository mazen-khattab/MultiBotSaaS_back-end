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

namespace SaaS.Application.Features.Bots.Queries.GetSubscribed
{
    public class GetSubscribedBotsQueryHandler : IRequestHandler<GetSubscribedBotsQuery, ApiResponse<List<BotDto>>>
    {
        private readonly IAppDbContext _context;

        public GetSubscribedBotsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<BotDto>>> Handle(GetSubscribedBotsQuery request, CancellationToken cancellationToken)
        {
            var userBots = await _context.UserBots
                .AsNoTracking()
                .Where(ub => ub.UserId == request.UserId)
                .Include(ub => ub.Bot)
                .OrderBy(ub => ub.BotId)
                .ToListAsync(cancellationToken);

            var dtos = userBots
                .Where(ub => ub.Bot != null)
                .Select(ub => ub.Bot!.ToDto(isSubscribed: true, subscribedAt: ub.PurchaseDate))
                .ToList();

            return ApiResponse<List<BotDto>>.Success(dtos, "Subscribed bots retrieved successfully.");
        }
    }
}
