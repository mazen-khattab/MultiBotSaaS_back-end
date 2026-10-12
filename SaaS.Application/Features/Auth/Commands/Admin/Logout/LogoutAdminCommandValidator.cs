using System;
using FluentValidation;

namespace SaaS.Application.Features.Auth.Commands.Admin.Logout
{
    public class LogoutAdminCommandValidator : AbstractValidator<LogoutAdminCommand>
    {
        public LogoutAdminCommandValidator()
        {
            RuleFor(x => x.AdminId)
                .NotEmpty().WithMessage("AdminId is required.")
                .NotEqual(Guid.Empty).WithMessage("A valid admin Id must be provided.");
        }
    }
}
