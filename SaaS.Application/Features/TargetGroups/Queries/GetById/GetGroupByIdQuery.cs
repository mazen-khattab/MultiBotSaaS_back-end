using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.TargetGroups.Queries.GetById
{
    public record GetGroupByIdQuery(Guid UserId, int BotId, int Id) : IRequest<ApiResponse<GroupDetailsDto>>;
}
