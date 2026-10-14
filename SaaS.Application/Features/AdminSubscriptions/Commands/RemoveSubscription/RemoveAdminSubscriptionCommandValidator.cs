using FluentValidation;

namespace SaaS.Application.Features.AdminSubscriptions.Commands.RemoveSubscription;

public sealed class RemoveAdminSubscriptionCommandValidator : AbstractValidator<RemoveAdminSubscriptionCommand>
{
    public RemoveAdminSubscriptionCommandValidator() => RuleFor(command => command.SubscriptionId).GreaterThan(0);
}
