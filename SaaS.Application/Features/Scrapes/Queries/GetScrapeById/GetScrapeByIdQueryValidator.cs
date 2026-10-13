using System;
using FluentValidation;

namespace SaaS.Application.Features.Scrapes.Queries.GetScrapeById
{
    public class GetScrapeByIdQueryValidator : AbstractValidator<GetScrapeByIdQuery>
    {
        public GetScrapeByIdQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.ScrapeId)
                .GreaterThan(0).WithMessage("ScrapeId must be greater than 0.");
        }
    }
}
