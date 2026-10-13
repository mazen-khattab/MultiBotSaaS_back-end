using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.ActivityLogs.Queries.GetActivityLogs;

public sealed record GetActivityLogsQuery(
    Guid UserId,
    int? BotId = null,
    string? RunId = null,
    string? LogLevel = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<ApiResponse<PaginatedResult<BotActivityLogDto>>>;
