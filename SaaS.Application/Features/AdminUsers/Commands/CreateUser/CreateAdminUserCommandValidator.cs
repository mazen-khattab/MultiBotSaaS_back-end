using FluentValidation;

namespace SaaS.Application.Features.AdminUsers.Commands.CreateUser;

public sealed class CreateAdminUserCommandValidator : AbstractValidator<CreateAdminUserCommand>
{
    public CreateAdminUserCommandValidator()
    {
        RuleFor(command => command.User.FullName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.User.Email).NotEmpty().MaximumLength(255).EmailAddress();
        RuleFor(command => command.User.Password).NotEmpty();
        RuleFor(command => command.User.PhoneNumber).NotNull();
    }
}
