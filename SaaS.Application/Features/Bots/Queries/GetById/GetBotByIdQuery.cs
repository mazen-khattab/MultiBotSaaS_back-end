using System;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Bots.Queries.GetById
{
    public record GetBotByIdQuery(Guid UserId, int BotId, bool RequireOwnership = false) : IRequest<ApiResponse<BotDto>>;
}
