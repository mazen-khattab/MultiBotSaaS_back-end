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
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.Scrapes.Queries.GetAllScrapes
{
    public class GetAllScrapesQueryHandler : IRequestHandler<GetAllScrapesQuery, ApiResponse<PaginatedResult<ScrapeDto>>>
    {
        private readonly IAppDbContext _context;
        private readonly IUserBotService _userBotService;

        public GetAllScrapesQueryHandler(IAppDbContext context, IUserBotService userBotService)
        {
            _context = context;
            _userBotService = userBotService;
        }

        public async Task<ApiResponse<PaginatedResult<ScrapeDto>>> Handle(GetAllScrapesQuery request, CancellationToken cancellationToken)
        {
            var hasOwnership = await _userBotService.CheckOwnershipAsync(request.UserId, request.BotId, cancellationToken);

            if (!hasOwnership)
            {
                return ApiResponse<PaginatedResult<ScrapeDto>>.Failure("User or bot not found", ErrorType.NotFound);
            }

            var query = _context.Scrapes
                .AsNoTracking()
                .Where(candidate => candidate.UserId == request.UserId && candidate.BotId == request.BotId);

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
