using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.Delete
{
    public class DeleteConnectedAccountCommandValidator : AbstractValidator<DeleteConnectedAccountCommand>
    {
        public DeleteConnectedAccountCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Id is required.")
                .GreaterThan(0)
                .WithMessage("A valid connected account Id must be provided.");
        }
    }
}
