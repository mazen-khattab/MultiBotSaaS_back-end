using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.Scrapes.Queries.GetScrapeById
{
    public class GetScrapeByIdQueryHandler : IRequestHandler<GetScrapeByIdQuery, ApiResponse<ScrapeDto>>
    {
        private readonly IAppDbContext _context;

        public GetScrapeByIdQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<ScrapeDto>> Handle(GetScrapeByIdQuery request, CancellationToken cancellationToken)
        {
            var scrape = await _context.Scrapes
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == request.ScrapeId && candidate.UserId == request.UserId, cancellationToken);

            if (scrape == null)
            {
                return ApiResponse<ScrapeDto>.Failure("Scrape not found.", ErrorType.NotFound);
            }

            return ApiResponse<ScrapeDto>.Success(scrape.ToDto(), "Scrape retrieved successfully.");
        }
    }
}
