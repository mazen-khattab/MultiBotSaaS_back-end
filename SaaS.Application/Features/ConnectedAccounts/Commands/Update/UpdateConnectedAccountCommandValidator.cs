using FluentValidation;
using SaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.Update
{
    public class UpdateConnectedAccountCommandValidator : AbstractValidator<UpdateConnectedAccountCommand>
    {
        public UpdateConnectedAccountCommandValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage("Connected account Id is required.")
                .GreaterThan(0)
                .WithMessage("A valid connected account Id must be provided.");

            RuleFor(x => x.AccountDto.Status)
                .NotEmpty()
                .WithMessage("Status is required.")
                .Must(BeAValidStatus)
                .WithMessage("Status must be a valid AccountStatus.");

            RuleFor(x => x.AccountDto.Cookies)
                .Must(IsValidJson).WithMessage("Cookies must be valid JSON.");
        }

        private bool IsValidJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool BeAValidStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            if (Enum.TryParse<AccountStatus>(status, true, out _))
                return true;

            return false;
        }
    }
}
