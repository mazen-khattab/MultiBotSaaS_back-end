using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Constants;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminBots.Commands.UpdateBot;

public sealed class UpdateAdminBotCommandHandler(IAppDbContext db)
    : IRequestHandler<UpdateAdminBotCommand, ApiResponse<AdminBotDto>>
{
    public async Task<ApiResponse<AdminBotDto>> Handle(UpdateAdminBotCommand request, CancellationToken cancellationToken)
    {
        var bot = await db.Bots.SingleOrDefaultAsync(bot => bot.Id == request.BotId, cancellationToken);
        if (bot is null)
            return ApiResponse<AdminBotDto>.Failure("Bot not found.", ErrorType.NotFound);
        // Codes identify deployed frontend modules. With no migration mechanism, keep the
        // original code even for unused bots and even if a normalized spelling was supplied.
        if (request.Bot.UiModuleCode is not null && UiModuleCodes.Normalize(request.Bot.UiModuleCode) != bot.UiModuleCode)
            return ApiResponse<AdminBotDto>.Failure("UiModuleCode cannot be changed after creation.", ErrorType.ConcurrencyConflict);
        bot.Name = request.Bot.Name.Trim();
        bot.Description = request.Bot.Description;
        bot.CurrentPrice = request.Bot.CurrentPrice;
        bot.CooldownMinutes = request.Bot.CooldownMinutes;
        await db.SaveChangesAsync(cancellationToken);
        return ApiResponse<AdminBotDto>.Success(bot.ToAdminDto(), "Bot updated successfully.");
    }
}
