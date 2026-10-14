using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Constants;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminBots.Commands.CreateBot;

public sealed class CreateAdminBotCommandHandler(IAppDbContext db, IAdminBotPersistenceService adminBotPersistenceService)
    : IRequestHandler<CreateAdminBotCommand, ApiResponse<AdminBotDto>>
{
    public async Task<ApiResponse<AdminBotDto>> Handle(CreateAdminBotCommand request, CancellationToken cancellationToken)
    {
        var code = UiModuleCodes.Normalize(request.Bot.UiModuleCode);
        if (await adminBotPersistenceService.CodeExists(code, cancellationToken))
            return ApiResponse<AdminBotDto>.Failure("UiModuleCode is already in use.", ErrorType.ConcurrencyConflict);
        var bot = new Bot
        {
            Name = request.Bot.Name.Trim(), Description = request.Bot.Description, CurrentPrice = request.Bot.CurrentPrice,
            UiModuleCode = code, CooldownMinutes = request.Bot.CooldownMinutes,
            IsActive = false, Status = "Inactive"
        };
        db.Bots.Add(bot);
        if (!await adminBotPersistenceService.TrySaveChangesAsync(cancellationToken))
            return ApiResponse<AdminBotDto>.Failure("UiModuleCode is already in use.", ErrorType.ConcurrencyConflict);
        return ApiResponse<AdminBotDto>.Success(bot.ToAdminDto(), "Bot created successfully.");
    }
}
