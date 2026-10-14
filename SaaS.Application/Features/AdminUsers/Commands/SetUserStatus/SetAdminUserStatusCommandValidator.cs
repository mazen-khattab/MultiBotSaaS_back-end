using FluentValidation;

namespace SaaS.Application.Features.AdminUsers.Commands.SetUserStatus;

public sealed class SetAdminUserStatusCommandValidator : AbstractValidator<SetAdminUserStatusCommand>
{
    public SetAdminUserStatusCommandValidator() => RuleFor(command => command.UserId).NotEmpty();
}
