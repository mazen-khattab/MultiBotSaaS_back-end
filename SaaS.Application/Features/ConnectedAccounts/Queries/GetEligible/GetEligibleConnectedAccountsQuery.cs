using System;
using System.Collections.Generic;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetEligible
{
    public record GetEligibleConnectedAccountsQuery(Guid UserId, int BotId) : IRequest<ApiResponse<List<ConnectedAccountDto>>>;
}
