using FluentValidation;
using SaaS.Domain.Constants;

namespace SaaS.Application.Features.AdminBots.Commands.CreateBot;

public sealed class CreateAdminBotCommandValidator : AbstractValidator<CreateAdminBotCommand>
{
    public CreateAdminBotCommandValidator()
    {
        RuleFor(command => command.Bot.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Bot.UiModuleCode).Must(UiModuleCodes.IsValid)
            .WithMessage("UiModuleCode is required and must contain 1 to 50 letters/numbers separated by single hyphens.");
        RuleFor(command => command.Bot.CurrentPrice).InclusiveBetween(0m, 9999999999999999.99m)
            .Must(price => decimal.Round(price, 2) == price).WithMessage("CurrentPrice must have at most two decimal places.");
        RuleFor(command => command.Bot.CooldownMinutes).GreaterThanOrEqualTo(0);
    }
}
