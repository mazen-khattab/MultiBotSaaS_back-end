using System;

namespace SaaS.Application.Common.Dtos
{
    public record ScrapeDto(
        int Id,
        int? BotId,
        int? GroupId,
        int? AccountId,
        int CollectedLeadsCount,
        string InfoJson,
        DateTime StartedAt,
        DateTime? EndedAt,
        string Status
    );
}
