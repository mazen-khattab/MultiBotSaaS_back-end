using System;
using MediatR;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Scrapes.Commands.Cancel
{
    public record CancelScrapeCommand(Guid UserId, int ScrapeId) : IRequest<ApiResponse<bool>>;
}
