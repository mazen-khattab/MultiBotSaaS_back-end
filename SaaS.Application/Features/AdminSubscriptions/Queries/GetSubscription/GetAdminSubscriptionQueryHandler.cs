using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminSubscriptions.Queries.GetSubscription;

public sealed class GetAdminSubscriptionQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAdminSubscriptionQuery, ApiResponse<AdminSubscriptionDto>>
{
    public async Task<ApiResponse<AdminSubscriptionDto>> Handle(GetAdminSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var assignment = await db.UserBots.AsNoTracking().Where(assignment => assignment.Id == request.SubscriptionId)
            .Select(AdminSubscriptionMapperExtension.Projection).SingleOrDefaultAsync(cancellationToken);
        return assignment is null ? ApiResponse<AdminSubscriptionDto>.Failure("Subscription not found.", ErrorType.NotFound)
            : ApiResponse<AdminSubscriptionDto>.Success(assignment, "Subscription retrieved successfully.");
    }
}
