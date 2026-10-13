using System;
using FluentValidation;

namespace SaaS.Application.Features.Scrapes.Queries.GetAllScrapes
{
    public class GetAllScrapesQueryValidator : AbstractValidator<GetAllScrapesQuery>
    {
        public GetAllScrapesQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.BotId)
                .GreaterThan(0).When(x => x.BotId.HasValue).WithMessage("BotId must be greater than 0.");

            RuleFor(x => x.PageNumber)
                .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
        }
    }
}
