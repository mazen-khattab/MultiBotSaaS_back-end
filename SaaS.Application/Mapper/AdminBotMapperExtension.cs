using System.Linq.Expressions;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Application.Mapper;

public static class AdminBotMapperExtension
{
    public static readonly Expression<Func<Bot, AdminBotDto>> Projection = bot => new AdminBotDto(
        Id: bot.Id,
        Name: bot.Name,
        Description: bot.Description,
        CurrentPrice: bot.CurrentPrice,
        UiModuleCode: bot.UiModuleCode,
        CooldownMinutes: bot.CooldownMinutes,
        IsActive: bot.IsActive,
        Status: bot.Status
    );

    public static AdminBotDto ToAdminDto(this Bot bot)
    {
        ArgumentNullException.ThrowIfNull(bot, nameof(bot));

        return new AdminBotDto(
            Id: bot.Id,
            Name: bot.Name,
            Description: bot.Description,
            CurrentPrice: bot.CurrentPrice,
            UiModuleCode: bot.UiModuleCode,
            CooldownMinutes: bot.CooldownMinutes,
            IsActive: bot.IsActive,
            Status: bot.Status
        );
    }

    public static List<AdminBotDto> ToAdminDtoList(this IEnumerable<Bot> bots)
    {
        ArgumentNullException.ThrowIfNull(bots, nameof(bots));

        return [.. bots.Select(b => b.ToAdminDto())];
    }
}