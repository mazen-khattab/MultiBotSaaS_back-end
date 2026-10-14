using FluentValidation;

namespace SaaS.Application.Features.AdminBots.Commands.SetBotStatus;

public sealed class SetAdminBotStatusCommandValidator : AbstractValidator<SetAdminBotStatusCommand>
{
    public SetAdminBotStatusCommandValidator() => RuleFor(command => command.BotId).GreaterThan(0);
}
