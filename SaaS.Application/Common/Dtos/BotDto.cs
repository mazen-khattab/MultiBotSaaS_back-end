using System;

namespace SaaS.Application.Common.Dtos
{
    public record BotDto(
        int Id,
        string Name,
        string? Description,
        decimal CurrentPrice,
        string UiModuleCode,
        int CooldownMinutes,
        bool IsActive,
        string Status,
        bool IsSubscribed,
        DateTime? SubscribedAt
    );
}
