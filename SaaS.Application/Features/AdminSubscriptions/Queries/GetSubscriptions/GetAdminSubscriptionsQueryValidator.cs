using FluentValidation;

namespace SaaS.Application.Features.AdminSubscriptions.Queries.GetSubscriptions;

public sealed class GetAdminSubscriptionsQueryValidator : AbstractValidator<GetAdminSubscriptionsQuery>
{
    public GetAdminSubscriptionsQueryValidator()
    {
        RuleFor(query => query.UserId).NotEqual(Guid.Empty).When(query => query.UserId.HasValue);
        RuleFor(query => query.BotId).GreaterThan(0).When(query => query.BotId.HasValue);
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query).Must(query => ((long)query.PageNumber - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested pagination offset is too large.");
    }
}
