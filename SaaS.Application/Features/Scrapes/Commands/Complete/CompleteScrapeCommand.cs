using System.Collections.Generic;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Scrapes.Commands.Complete
{
    public record CompleteScrapeCommand(int ScrapeId, List<ScrapedLeadDto> Leads) : IRequest<ApiResponse<bool>>;
}
