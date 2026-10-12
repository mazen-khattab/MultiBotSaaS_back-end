using System;
using FluentValidation;

namespace SaaS.Application.Features.Settings.Commands.Manage
{
    public class ManageUserSettingsCommandValidator : AbstractValidator<ManageUserSettingsCommand>
    {
        public ManageUserSettingsCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.DailyLeadLimit)
                .GreaterThanOrEqualTo(0).WithMessage("DailyLeadLimit must be greater than or equal to 0.");
        }
    }
}
