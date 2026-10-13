using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetAll
{
    public record GetAllConnectedAccountsQuery(Guid UserId, int BotId) : IRequest<ApiResponse<List<ConnectedAccountDto>>>;
}
