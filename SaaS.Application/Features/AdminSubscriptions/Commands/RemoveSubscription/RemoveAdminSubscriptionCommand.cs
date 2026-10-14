using MediatR;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminSubscriptions.Commands.RemoveSubscription;

public sealed record RemoveAdminSubscriptionCommand(int SubscriptionId) : IRequest<ApiResponse<bool>>;
