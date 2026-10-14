using FluentValidation;

namespace SaaS.Application.Features.AdminSubscriptions.Queries.GetSubscription;

public sealed class GetAdminSubscriptionQueryValidator : AbstractValidator<GetAdminSubscriptionQuery>
{
    public GetAdminSubscriptionQueryValidator() => RuleFor(query => query.SubscriptionId).GreaterThan(0);
}
