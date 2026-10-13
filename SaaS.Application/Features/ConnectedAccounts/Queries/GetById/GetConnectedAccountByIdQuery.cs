using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetById
{
    public record GetConnectedAccountByIdQuery(Guid UserId, int BotId, int Id) : IRequest<ApiResponse<ConnectedAccountDetailsDto>>;
}
