using System;
using MediatR;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Runs.Commands.Cancel
{
    public record CancelRunCommand(Guid UserId, int RunId) : IRequest<ApiResponse<bool>>;
}
