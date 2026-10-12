using MediatR;
using SaaS.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.Delete
{
    public record DeleteConnectedAccountCommand(Guid UserId, int Id) : IRequest<ApiResponse<string>>;
}
