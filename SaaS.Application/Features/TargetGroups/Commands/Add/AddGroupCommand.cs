using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using System;

namespace SaaS.Application.Features.TargetGroups.Commands.Add
{
    public record AddGroupCommand(Guid UserId, AddGroupDto GroupDto) : IRequest<ApiResponse<int>>;
}
