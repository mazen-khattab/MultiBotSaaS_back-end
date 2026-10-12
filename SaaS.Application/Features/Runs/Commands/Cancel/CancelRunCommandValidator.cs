using System;
using FluentValidation;

namespace SaaS.Application.Features.Runs.Commands.Cancel
{
    public class CancelRunCommandValidator : AbstractValidator<CancelRunCommand>
    {
        public CancelRunCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.RunId)
                .GreaterThan(0).WithMessage("RunId must be greater than 0.");
        }
    }
}
