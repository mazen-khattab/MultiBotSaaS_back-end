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

namespace SaaS.Application.Features.Bots.Queries.GetAll
{
    public class GetAllBotsQueryHandler : IRequestHandler<GetAllBotsQuery, ApiResponse<List<BotDto>>>
    {
        private readonly IAppDbContext _context;

        public GetAllBotsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<BotDto>>> Handle(GetAllBotsQuery request, CancellationToken cancellationToken)
        {
            var bots = await _context.Bots
                .AsNoTracking()
                .Where(b => b.IsActive)
                .Include(b => b.UserBots.Where(ub => ub.UserId == request.UserId))
                .OrderBy(b => b.Id)
                .ToListAsync(cancellationToken);

            var dtos = bots.Select(b =>
            {
                var userBot = b.UserBots.FirstOrDefault();
                return b.ToDto(isSubscribed: userBot != null, subscribedAt: userBot?.PurchaseDate);
            }).ToList();

            return ApiResponse<List<BotDto>>.Success(dtos, "Available bots retrieved successfully.");
        }
    }
}
