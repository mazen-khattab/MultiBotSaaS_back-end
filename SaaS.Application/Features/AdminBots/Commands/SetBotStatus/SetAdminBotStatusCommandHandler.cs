using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminBots.Commands.SetBotStatus;

public sealed class SetAdminBotStatusCommandHandler(IAppDbContext db)
    : IRequestHandler<SetAdminBotStatusCommand, ApiResponse<AdminBotDto>>
{
    public async Task<ApiResponse<AdminBotDto>> Handle(SetAdminBotStatusCommand request, CancellationToken cancellationToken)
    {
        var bot = await db.Bots.SingleOrDefaultAsync(bot => bot.Id == request.BotId, cancellationToken);
        if (bot is null)
            return ApiResponse<AdminBotDto>.Failure("Bot not found.", ErrorType.NotFound);
        bot.IsActive = request.IsActive;
        bot.Status = request.IsActive ? "Active" : "Inactive";
        await db.SaveChangesAsync(cancellationToken);
        return ApiResponse<AdminBotDto>.Success(bot.ToAdminDto(), request.IsActive
            ? "Bot activated successfully." : "Bot deactivated successfully.");
    }
}
