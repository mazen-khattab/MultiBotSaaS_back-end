using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminSubscriptions.Queries.GetSubscription;

public sealed record GetAdminSubscriptionQuery(int SubscriptionId) : IRequest<ApiResponse<AdminSubscriptionDto>>;
