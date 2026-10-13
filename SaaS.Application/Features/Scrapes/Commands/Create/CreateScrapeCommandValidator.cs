using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;

namespace SaaS.Application.Features.Scrapes.Commands.Create
{
    public class CreateScrapeCommandValidator : AbstractValidator<CreateScrapeCommand>
    {
        public CreateScrapeCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEqual(Guid.Empty).WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.CreateScrapeDto.BotId)
                .NotEmpty().WithMessage("BotId is required.")
                .GreaterThan(0)
                .WithMessage("A valid connected account Id must be provided.");

            RuleFor(x => x.CreateScrapeDto.ConnectedAccountId)
                .GreaterThan(0).WithMessage("ConnectedAccountId is required.");

            RuleFor(x => x.CreateScrapeDto.InfoJson)
                .NotEmpty().WithMessage("InfoJson is required.")
                .Must(IsValidJson).WithMessage("InfoJson must be valid JSON.");
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
