using System;
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

namespace SaaS.Application.Features.Bots.Queries.GetById
{
    public class GetBotByIdQueryHandler : IRequestHandler<GetBotByIdQuery, ApiResponse<BotDto>>
    {
        private readonly IAppDbContext _context;

        public GetBotByIdQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<BotDto>> Handle(GetBotByIdQuery request, CancellationToken cancellationToken)
        {
            var bot = await _context.Bots
                .AsNoTracking()
                .Include(b => b.UserBots.Where(ub => ub.UserId == request.UserId))
                .FirstOrDefaultAsync(b => b.Id == request.BotId, cancellationToken);

            if (bot == null)
            {
                return ApiResponse<BotDto>.Failure("Bot not found.", ErrorType.NotFound);
            }

            var userBot = bot.UserBots.FirstOrDefault();
            var isSubscribed = userBot != null;

            if (request.RequireOwnership && !isSubscribed)
            {
                return ApiResponse<BotDto>.Failure("User is not subscribed to this bot.", ErrorType.NotFound);
            }

            return ApiResponse<BotDto>.Success(bot.ToDto(isSubscribed: isSubscribed, subscribedAt: userBot?.PurchaseDate), "Bot details retrieved successfully.");
        }
    }
}
