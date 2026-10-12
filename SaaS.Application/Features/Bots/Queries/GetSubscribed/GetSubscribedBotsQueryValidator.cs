using System;
using FluentValidation;

namespace SaaS.Application.Features.Bots.Queries.GetSubscribed
{
    public class GetSubscribedBotsQueryValidator : AbstractValidator<GetSubscribedBotsQuery>
    {
        public GetSubscribedBotsQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");
        }
    }
}
