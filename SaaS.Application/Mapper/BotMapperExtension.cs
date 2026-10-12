using System;
using System.Collections.Generic;
using System.Linq;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Application.Mapper
{
    public static class BotMapperExtension
    {
        public static BotDto ToDto(this Bot bot, bool isSubscribed = false, DateTime? subscribedAt = null)
        {
            ArgumentNullException.ThrowIfNull(bot, nameof(bot));

            return new BotDto(
                Id: bot.Id,
                Name: bot.Name,
                Description: bot.Description,
                CurrentPrice: bot.CurrentPrice,
                UiModuleCode: bot.UiModuleCode,
                CooldownMinutes: bot.CooldownMinutes,
                IsActive: bot.IsActive,
                Status: bot.Status,
                IsSubscribed: isSubscribed,
                SubscribedAt: subscribedAt
            );
        }

        public static List<BotDto> ToDtoList(this IEnumerable<Bot> bots)
        {
            ArgumentNullException.ThrowIfNull(bots, nameof(bots));

            return [.. bots.Select(b => b.ToDto())];
        }
    }
}
