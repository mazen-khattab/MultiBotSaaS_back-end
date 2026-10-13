using System.Collections.Generic;
using FluentValidation;

namespace SaaS.Application.Features.Scrapes.Commands.Complete
{
    public class CompleteScrapeCommandValidator : AbstractValidator<CompleteScrapeCommand>
    {
        public CompleteScrapeCommandValidator()
        {
            RuleFor(x => x.Leads)
                .NotNull().WithMessage("Leads list must be provided (can be empty).");
        }
    }
}
