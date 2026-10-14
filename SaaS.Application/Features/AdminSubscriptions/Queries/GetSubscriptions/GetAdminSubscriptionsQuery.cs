using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminSubscriptions.Queries.GetSubscriptions;

public sealed record GetAdminSubscriptionsQuery(Guid? UserId = null, int? BotId = null, bool? IsActive = null,
    int PageNumber = 1, int PageSize = 10) : IRequest<ApiResponse<PaginatedResult<AdminSubscriptionDto>>>;
