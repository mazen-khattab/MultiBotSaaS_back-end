using System;
using System.Collections.Generic;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Bots.Queries.GetAll
{
    public record GetAllBotsQuery(Guid UserId) : IRequest<ApiResponse<List<BotDto>>>;
}
