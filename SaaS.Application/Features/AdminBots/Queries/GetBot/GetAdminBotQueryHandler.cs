using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminBots.Queries.GetBot;

public sealed class GetAdminBotQueryHandler(IAppDbContext db) : IRequestHandler<GetAdminBotQuery, ApiResponse<AdminBotDto>>
{
    public async Task<ApiResponse<AdminBotDto>> Handle(GetAdminBotQuery request, CancellationToken cancellationToken)
    {
        var bot = await db.Bots.AsNoTracking().Where(bot => bot.Id == request.BotId)
            .Select(AdminBotMapperExtension.Projection).SingleOrDefaultAsync(cancellationToken);
        return bot is null ? ApiResponse<AdminBotDto>.Failure("Bot not found.", ErrorType.NotFound)
            : ApiResponse<AdminBotDto>.Success(bot, "Bot retrieved successfully.");
    }
}
