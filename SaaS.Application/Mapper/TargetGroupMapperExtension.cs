using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Mapper
{
    public static class TargetGroupMapperExtension
    {
        public static GroupDto ToDto(this TargetGroup targetGroup)
        {
            ArgumentNullException.ThrowIfNull(targetGroup, nameof(targetGroup));

            return new GroupDto(
                targetGroup.Id,
                targetGroup.GroupName,
                targetGroup.GroupUrl,
                targetGroup.IsActive
            );
        }

        public static List<GroupDto> ToDtoList(this IEnumerable<TargetGroup> targetGroups)
        {
            ArgumentNullException.ThrowIfNull(targetGroups, nameof(targetGroups));

            return [.. targetGroups.Select(tg => tg.ToDto())];
        }

        public static GroupDetailsDto ToDetailsDto(this TargetGroup targetGroup, int leadsCount, int scrapesCount)
        {
            ArgumentNullException.ThrowIfNull(targetGroup, nameof(targetGroup));

            return new GroupDetailsDto(
                targetGroup.Id,
                targetGroup.GroupName,
                targetGroup.GroupUrl,
                targetGroup.ConfigJson,
                targetGroup.IsActive,
                leadsCount,
                scrapesCount
            );
        }

        public static TargetGroup FromDto(this TargetGroup targetGroup, UpdateGroupDto groupDto)
        {
            ArgumentNullException.ThrowIfNull(targetGroup, nameof(targetGroup));
            ArgumentNullException.ThrowIfNull(groupDto, nameof(groupDto));

            targetGroup.GroupName = groupDto.GroupName;
            targetGroup.GroupUrl = groupDto.GroupUrl;
            targetGroup.IsActive = groupDto.IsActive;
            targetGroup.ConfigJson = targetGroup.ConfigJson;
            targetGroup.LastCursor = targetGroup.LastCursor;

            return targetGroup;
        }
    }
}
