using System;
using FluentValidation;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetEligible
{
    public class GetEligibleConnectedAccountsQueryValidator : AbstractValidator<GetEligibleConnectedAccountsQuery>
    {
        public GetEligibleConnectedAccountsQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.BotId)
                .GreaterThan(0).WithMessage("BotId must be greater than 0.");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Type must be a valid AccountType.");
        }
    }
}
