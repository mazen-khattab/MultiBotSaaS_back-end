using FluentValidation;

namespace SaaS.Application.Features.AdminBots.Queries.GetBot;

public sealed class GetAdminBotQueryValidator : AbstractValidator<GetAdminBotQuery>
{
    public GetAdminBotQueryValidator() => RuleFor(query => query.BotId).GreaterThan(0);
}
