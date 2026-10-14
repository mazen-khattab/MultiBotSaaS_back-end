using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminSubscriptions.Commands.AssignSubscription;

public sealed record AssignAdminSubscriptionCommand(AssignAdminSubscriptionDto Assignment) : IRequest<ApiResponse<AdminSubscriptionDto>>;
