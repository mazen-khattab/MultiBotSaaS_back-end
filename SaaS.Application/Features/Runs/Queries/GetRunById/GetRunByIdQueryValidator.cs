using System;
using FluentValidation;

namespace SaaS.Application.Features.Runs.Queries.GetRunById
{
    public class GetRunByIdQueryValidator : AbstractValidator<GetRunByIdQuery>
    {
        public GetRunByIdQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.RunId)
                .GreaterThan(0).WithMessage("RunId must be greater than 0.");
        }
    }
}
