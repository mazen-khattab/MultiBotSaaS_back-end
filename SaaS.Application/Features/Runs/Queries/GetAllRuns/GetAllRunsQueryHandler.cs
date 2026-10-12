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

namespace SaaS.Application.Features.Runs.Queries.GetAllRuns
{
    public class GetAllRunsQueryHandler : IRequestHandler<GetAllRunsQuery, ApiResponse<PaginatedResult<RunDto>>>
    {
        private readonly IAppDbContext _context;

        public GetAllRunsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<PaginatedResult<RunDto>>> Handle(GetAllRunsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Runs
                .AsNoTracking()
                .Where(r => r.UserId == request.UserId);

            if (request.BotId.HasValue && request.BotId.Value > 0)
            {
                query = query.Where(r => r.BotId == request.BotId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.StatusFilter))
            {
                query = query.Where(r => r.Status == request.StatusFilter);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(r => r.StartedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(r => r.ToDto())
                .ToListAsync(cancellationToken);

            var paginated = new PaginatedResult<RunDto>
            {
                Items = items,
                TotalItems = totalCount,
                TotalPages = totalPages,
                CurrentPage = pageNumber
            };

            return ApiResponse<PaginatedResult<RunDto>>.Success(paginated, "Runs retrieved successfully.");
        }
    }
}
