using System;
using FluentValidation;

namespace SaaS.Application.Features.Settings.Queries
{
    public class GetUserSettingsQueryValidator : AbstractValidator<GetUserSettingsQuery>
    {
        public GetUserSettingsQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");
        }
    }
}
