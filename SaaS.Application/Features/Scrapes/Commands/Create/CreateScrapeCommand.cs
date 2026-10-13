using System;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Scrapes.Commands.Create
{
    public record CreateScrapeCommand(Guid UserId, CreateScrapeDto CreateScrapeDto) : IRequest<ApiResponse<int>>;
}
