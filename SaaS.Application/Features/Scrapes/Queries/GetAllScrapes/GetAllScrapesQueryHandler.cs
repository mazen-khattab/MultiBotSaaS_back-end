using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;

namespace SaaS.Application.Features.Scrapes.Queries.GetAllScrapes
{
    public class GetAllScrapesQueryHandler : IRequestHandler<GetAllScrapesQuery, ApiResponse<PaginatedResult<ScrapeDto>>>
    {
        private readonly IAppDbContext _context;

        public GetAllScrapesQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<PaginatedResult<ScrapeDto>>> Handle(GetAllScrapesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Scrapes
                .AsNoTracking()
                .Where(candidate => candidate.UserId == request.UserId);

            if (request.BotId.HasValue && request.BotId.Value > 0)
            {
                query = query.Where(candidate => candidate.BotId == request.BotId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.StatusFilter))
            {
                query = query.Where(candidate => candidate.Status == request.StatusFilter);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(candidate => candidate.StartedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(candidate => candidate.ToDto())
                .ToListAsync(cancellationToken);

            var paginated = new PaginatedResult<ScrapeDto>
            {
                Items = items,
                TotalItems = totalCount,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };

            return ApiResponse<PaginatedResult<ScrapeDto>>.Success(paginated, "Scrapes retrieved successfully.");
        }
    }
}
