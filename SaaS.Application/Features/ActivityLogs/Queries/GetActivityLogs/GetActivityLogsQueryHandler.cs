using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.ActivityLogs.Queries.GetActivityLogs;

public sealed class GetActivityLogsQueryHandler(IAppDbContext db, IUserBotService _userBotService) : IRequestHandler<GetActivityLogsQuery, ApiResponse<PaginatedResult<BotActivityLogDto>>>
{
    public async Task<ApiResponse<PaginatedResult<BotActivityLogDto>>> Handle(
        GetActivityLogsQuery request, CancellationToken cancellationToken)
    {
        if (request.BotId.HasValue && !await _userBotService.CheckOwnershipAsync(request.UserId, request.BotId.Value, cancellationToken))
        {
            return ApiResponse<PaginatedResult<BotActivityLogDto>>.Failure("User or bot not found.", ErrorType.NotFound);
        }

        // Worker-authenticated UserId is the existing owner of a correlation. For associated
        // logs also require bot ownership, including when no bot filter was supplied.
        var logs = db.BotActivityLogs
            .AsNoTracking()
            .Where(log => log.UserId == request.UserId &&
            (log.BotId == null || db.UserBots.Any(subscription => subscription.UserId == request.UserId && subscription.BotId == log.BotId)));

        // CorrelationId is opaque: never guess that numeric correlations are Job/Scrape IDs.
        // Check before the other filters so a valid correlation with no level matches is empty.
        if (request.RunId is not null)
        {
            if (!await logs.AnyAsync(log => log.CorrelationId == request.RunId, cancellationToken))
                return ApiResponse<PaginatedResult<BotActivityLogDto>>.Failure("Activity log run not found.", ErrorType.NotFound);
            logs = logs.Where(log => log.CorrelationId == request.RunId);
        }

        if (request.BotId.HasValue)
        {
            logs = logs.Where(log => log.BotId == request.BotId.Value);
        }
        if (request.LogLevel is not null)
        {
            logs = logs.Where(log => log.LogLevel == request.LogLevel);
        }

        var totalItems = await logs.CountAsync(cancellationToken);

        var items = await logs.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(log => new BotActivityLogDto(log.Id, log.CorrelationId, log.LogLevel, log.Message, log.CreatedAt))
            .ToListAsync(cancellationToken);

        return ApiResponse<PaginatedResult<BotActivityLogDto>>.Success(new PaginatedResult<BotActivityLogDto>
        {
            Items = items,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)request.PageSize),
            CurrentPage = request.PageNumber
        }, "Activity logs retrieved successfully.");
    }
}
