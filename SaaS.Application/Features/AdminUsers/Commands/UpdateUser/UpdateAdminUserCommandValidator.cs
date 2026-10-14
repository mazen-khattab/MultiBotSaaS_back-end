using FluentValidation;

namespace SaaS.Application.Features.AdminUsers.Commands.UpdateUser;

public sealed class UpdateAdminUserCommandValidator : AbstractValidator<UpdateAdminUserCommand>
{
    public UpdateAdminUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.User.FullName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.User.Email).NotEmpty().MaximumLength(255).EmailAddress();
        RuleFor(command => command.User.PhoneNumber).NotNull();
    }
}
