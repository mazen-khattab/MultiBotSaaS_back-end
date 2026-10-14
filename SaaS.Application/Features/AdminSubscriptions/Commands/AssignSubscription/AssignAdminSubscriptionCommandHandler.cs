using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminSubscriptions.Commands.AssignSubscription;

public sealed class AssignAdminSubscriptionCommandHandler(IAppDbContext db, IAdminSubscriptionPersistenceService persistence)
    : IRequestHandler<AssignAdminSubscriptionCommand, ApiResponse<AdminSubscriptionDto>>
{
    public async Task<ApiResponse<AdminSubscriptionDto>> Handle(AssignAdminSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var userId = request.Assignment.UserId;
        var botId = request.Assignment.BotId;
        if (!await db.Users.AnyAsync(user => user.Id == userId, cancellationToken))
            return ApiResponse<AdminSubscriptionDto>.Failure("User not found.", ErrorType.NotFound);
        if (!await db.Bots.AnyAsync(bot => bot.Id == botId, cancellationToken))
            return ApiResponse<AdminSubscriptionDto>.Failure("Bot not found.", ErrorType.NotFound);
        if (await persistence.AssignmentExistsAsync(userId, botId, cancellationToken))
            return ApiResponse<AdminSubscriptionDto>.Failure("User is already assigned to this bot.", ErrorType.ConcurrencyConflict);

        // Retain the legacy purchase columns without implying a charge or creating a transaction.
        var assignment = new UserBot { UserId = userId, BotId = botId, PurchasePrice = 0m, PurchaseDate = DateTime.UtcNow };
        db.UserBots.Add(assignment);
        if (!await persistence.TrySaveChangesAsync(cancellationToken))
            return ApiResponse<AdminSubscriptionDto>.Failure("User is already assigned to this bot.", ErrorType.ConcurrencyConflict);
        var dto = await db.UserBots.AsNoTracking().Where(candidate => candidate.Id == assignment.Id)
            .Select(AdminSubscriptionMapperExtension.Projection).SingleAsync(cancellationToken);
        return ApiResponse<AdminSubscriptionDto>.Success(dto, "Bot assigned successfully.");
    }
}
