using System;
using System.Collections.Generic;
using System.Linq;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Application.Mapper
{
    public static class RunMapperExtension
    {
        public static RunDto ToDto(this Run run)
        {
            ArgumentNullException.ThrowIfNull(run, nameof(run));

            return new RunDto(
                Id: run.Id,
                BotId: run.BotId,
                GroupId: run.GroupId,
                AccountId: run.AccountId,
                CollectedLeadsCount: run.CollectedLeadsCount,
                InfoJson: run.InfoJson,
                StartedAt: run.StartedAt,
                EndedAt: run.EndedAt,
                Status: run.Status
            );
        }

        public static List<RunDto> ToDtoList(this IEnumerable<Run> runs)
        {
            ArgumentNullException.ThrowIfNull(runs, nameof(runs));

            return [.. runs.Select(r => r.ToDto())];
        }
    }
}
