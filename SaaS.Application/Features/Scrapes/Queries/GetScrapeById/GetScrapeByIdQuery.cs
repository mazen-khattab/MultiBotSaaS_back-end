using System;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Scrapes.Queries.GetScrapeById
{
    public record GetScrapeByIdQuery(Guid UserId, int ScrapeId) : IRequest<ApiResponse<ScrapeDto>>;
}
