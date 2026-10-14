using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminSubscriptions.Commands.RemoveSubscription;

public sealed class RemoveAdminSubscriptionCommandHandler(IAppDbContext db)
    : IRequestHandler<RemoveAdminSubscriptionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RemoveAdminSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var assignment = await db.UserBots.SingleOrDefaultAsync(assignment => assignment.Id == request.SubscriptionId, cancellationToken);
        if (assignment is null)
            return ApiResponse<bool>.Failure("Subscription not found.", ErrorType.NotFound);
        // Existing ownership queries use row presence. Deleting just this relationship
        // revokes access without changing the user, bot or their other resources.
        db.UserBots.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Success(true, "Subscription removed successfully.");
    }
}
