using System;
using System.Collections.Generic;
using System.Linq;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Application.Mapper
{
    public static class ScrapeMapperExtension
    {
        public static ScrapeDto ToDto(this Scrape scrape)
        {
            ArgumentNullException.ThrowIfNull(scrape, nameof(scrape));

            return new ScrapeDto(
                Id: scrape.Id,
                BotId: scrape.BotId,
                GroupId: scrape.GroupId,
                AccountId: scrape.AccountId,
                CollectedLeadsCount: scrape.CollectedLeadsCount,
                InfoJson: scrape.InfoJson,
                StartedAt: scrape.StartedAt,
                EndedAt: scrape.EndedAt,
                Status: scrape.Status
            );
        }

        public static List<ScrapeDto> ToDtoList(this IEnumerable<Scrape> scrapes)
        {
            ArgumentNullException.ThrowIfNull(scrapes, nameof(scrapes));

            return [.. scrapes.Select(candidate => candidate.ToDto())];
        }
    }
}
