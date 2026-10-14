using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;

namespace SaaS.Application.Features.AdminSubscriptions.Queries.GetSubscriptions;

public sealed class GetAdminSubscriptionsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAdminSubscriptionsQuery, ApiResponse<PaginatedResult<AdminSubscriptionDto>>>
{
    public async Task<ApiResponse<PaginatedResult<AdminSubscriptionDto>>> Handle(GetAdminSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        var assignments = db.UserBots.AsNoTracking();
        if (request.UserId.HasValue)
            assignments = assignments.Where(assignment => assignment.UserId == request.UserId.Value);
        if (request.BotId.HasValue)
            assignments = assignments.Where(assignment => assignment.BotId == request.BotId.Value);
        // There is no stored inactive state. Removed assignments no longer grant ownership.
        if (request.IsActive == false)
            assignments = assignments.Where(assignment => false);

        var count = await assignments.CountAsync(cancellationToken);
        var items = await assignments.OrderByDescending(assignment => assignment.PurchaseDate)
            .ThenByDescending(assignment => assignment.Id).Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize).Select(AdminSubscriptionMapperExtension.Projection).ToListAsync(cancellationToken);
        return ApiResponse<PaginatedResult<AdminSubscriptionDto>>.Success(new PaginatedResult<AdminSubscriptionDto>
        {
            Items = items, TotalItems = count, TotalPages = (int)Math.Ceiling(count / (double)request.PageSize),
            CurrentPage = request.PageNumber
        }, "Subscriptions retrieved successfully.");
    }
}
