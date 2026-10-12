using System;
using FluentValidation;

namespace SaaS.Application.Features.Bots.Queries.GetAll
{
    public class GetAllBotsQueryValidator : AbstractValidator<GetAllBotsQuery>
    {
        public GetAllBotsQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");
        }
    }
}
