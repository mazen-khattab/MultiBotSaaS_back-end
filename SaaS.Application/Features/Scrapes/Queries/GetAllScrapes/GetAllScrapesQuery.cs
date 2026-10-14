using System;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Scrapes.Queries.GetAllScrapes
{
    public record GetAllScrapesQuery(Guid UserId, int BotId, int PageNumber = 1, int PageSize = 10, string? StatusFilter = null) : IRequest<ApiResponse<PaginatedResult<ScrapeDto>>>;
}
