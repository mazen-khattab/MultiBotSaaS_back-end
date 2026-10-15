using FluentValidation;

namespace SaaS.Application.Features.TargetGroups.Commands.Delete
{
    public class DeleteGroupCommandValidator : AbstractValidator<DeleteGroupCommand>
    {
        public DeleteGroupCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");

            RuleFor(x => x.Id)
                .GreaterThan(0)
                .WithMessage("A valid group Id must be provided.");
        }
    }
}
