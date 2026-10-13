using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.Add
{
    public class AddConnectedAccountValidator : AbstractValidator<AddConnectedAccountCommand>
    {
        public AddConnectedAccountValidator()
        {
            RuleFor(x => x.UserId)
                .NotEqual(Guid.Empty)
                .WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.AccountDto.BotId)
                .NotEmpty().WithMessage("BotId is required.")
                .GreaterThan(0)
                .WithMessage("A valid connected account Id must be provided.");

            RuleFor(x => x.AccountDto.DisplayName)
                .NotEmpty().WithMessage("DisplayName is required.");

            RuleFor(x => x.AccountDto.CookiesPayload)
                .Must(IsValidJson).WithMessage("CookiesPayload must be valid JSON.");
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

    }
}
