using System;
using System.Collections.Generic;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetEligible
{
    public record GetEligibleConnectedAccountsQuery(Guid UserId, int BotId, AccountType Type = AccountType.SCRAPING) : IRequest<ApiResponse<List<ConnectedAccountDto>>>;
}
