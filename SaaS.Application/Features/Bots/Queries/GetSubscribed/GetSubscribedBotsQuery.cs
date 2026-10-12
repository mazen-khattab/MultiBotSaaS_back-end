using System;
using System.Collections.Generic;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Bots.Queries.GetSubscribed
{
    public record GetSubscribedBotsQuery(Guid UserId) : IRequest<ApiResponse<List<BotDto>>>;
}
