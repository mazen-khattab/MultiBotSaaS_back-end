using FluentValidation;

namespace SaaS.Application.Features.Leads.Commands.RemoveLead;

public class RemoveLeadCommandValidator : AbstractValidator<RemoveLeadCommand>
{
    public RemoveLeadCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty().WithMessage("UserId is required.");
        RuleFor(command => command.LeadId).GreaterThan(0).WithMessage("LeadId must be greater than 0.");
    }
}
