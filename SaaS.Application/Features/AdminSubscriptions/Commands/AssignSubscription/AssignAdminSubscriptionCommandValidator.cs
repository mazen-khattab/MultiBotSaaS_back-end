using FluentValidation;

namespace SaaS.Application.Features.AdminSubscriptions.Commands.AssignSubscription;

public sealed class AssignAdminSubscriptionCommandValidator : AbstractValidator<AssignAdminSubscriptionCommand>
{
    public AssignAdminSubscriptionCommandValidator()
    {
        RuleFor(command => command.Assignment.UserId).NotEmpty();
        RuleFor(command => command.Assignment.BotId).GreaterThan(0);
    }
}
