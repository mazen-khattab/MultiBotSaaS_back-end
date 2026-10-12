using System;
using FluentValidation;

namespace SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin
{
    public class GetCurrentAdminQueryValidator : AbstractValidator<GetCurrentAdminQuery>
    {
        public GetCurrentAdminQueryValidator()
        {
            RuleFor(x => x.AdminId)
                .NotEmpty().WithMessage("AdminId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid admin Id must be provided.");
        }
    }
}
