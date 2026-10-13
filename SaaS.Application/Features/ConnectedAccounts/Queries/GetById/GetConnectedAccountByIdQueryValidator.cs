using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetById
{
    public class GetConnectedAccountByIdQueryValidator : AbstractValidator<GetConnectedAccountByIdQuery>
    {
        public GetConnectedAccountByIdQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.BotId)
                .GreaterThan(0).WithMessage("BotId must be greater than 0.");

            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Id is required.")
                .GreaterThan(0)
                .WithMessage("A valid connected account Id must be provided.");
        }
    }
}
