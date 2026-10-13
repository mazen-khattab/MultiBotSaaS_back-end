using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using System;
using System.Collections.Generic;

namespace SaaS.Application.Features.TargetGroups.Queries.GetAll
{
    public record GetAllGroupsQuery(Guid UserId, int BotId) : IRequest<ApiResponse<List<GroupDto>>>;
}
