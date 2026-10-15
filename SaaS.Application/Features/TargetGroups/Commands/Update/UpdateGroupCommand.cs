using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.TargetGroups.Commands.Update
{
    public record UpdateGroupCommand(Guid UserId, int Id, UpdateGroupDto GroupDto) : IRequest<ApiResponse<bool>>;
}
