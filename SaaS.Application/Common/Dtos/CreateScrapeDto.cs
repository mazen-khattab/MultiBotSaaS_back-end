using System;

namespace SaaS.Application.Common.Dtos
{
    public record CreateScrapeDto(int BotId, int ConnectedAccountId, int? TargetGroupId, string InfoJson);
}
