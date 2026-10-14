using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;

namespace SaaS.Application.Features.AdminBots.Queries.GetBots;

public sealed class GetAdminBotsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAdminBotsQuery, ApiResponse<PaginatedResult<AdminBotDto>>>
{
    public async Task<ApiResponse<PaginatedResult<AdminBotDto>>> Handle(GetAdminBotsQuery request, CancellationToken cancellationToken)
    {
        var bots = db.Bots.AsNoTracking();
        if (request.IsActive.HasValue)
            bots = bots.Where(bot => bot.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            bots = bots.Where(bot => bot.Name.Contains(search) || bot.UiModuleCode.Contains(search));
        }
        var count = await bots.CountAsync(cancellationToken);
        var items = await bots.OrderBy(bot => bot.Id).Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize).Select(AdminBotMapperExtension.Projection).ToListAsync(cancellationToken);
        return ApiResponse<PaginatedResult<AdminBotDto>>.Success(new PaginatedResult<AdminBotDto>
        {
            Items = items, TotalItems = count, TotalPages = (int)Math.Ceiling(count / (double)request.PageSize),
            CurrentPage = request.PageNumber
        }, "Bots retrieved successfully.");
    }
}
