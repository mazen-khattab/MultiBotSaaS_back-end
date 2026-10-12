using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.Update
{
    public record UpdateConnectedAccountCommand(Guid UserId, int Id, UpdateConnectedAccountDto AccountDto) : IRequest<ApiResponse<Guid>>;
}
