using FluentValidation;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Application.Features.Leads.Commands.UpdateLead;

public class UpdateLeadCommandValidator : AbstractValidator<UpdateLeadCommand>
{
    public UpdateLeadCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty().WithMessage("UserId is required.");
        RuleFor(command => command.LeadId).GreaterThan(0).WithMessage("LeadId must be greater than 0.");
        RuleFor(command => command.LeadDto).NotNull().WithMessage("Lead data is required.");

        // Match the database column limits and avoid dereferencing a missing DTO.
        When(command => command.LeadDto is not null, () =>
        {
            RuleFor(command => command.LeadDto.ProfileName)
                .NotEmpty().WithMessage("ProfileName is required.")
                .MaximumLength(255);

            RuleFor(command => command.LeadDto.ProfileUrl)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("ProfileUrl is required.")
                .MaximumLength(450)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .WithMessage("ProfileUrl must be an absolute HTTP or HTTPS URL.");

            // Comparing supported names also rejects numeric enum values such as "1".
            RuleFor(command => command.LeadDto.Status)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Status is required.")
                .Must(status => Enum.GetValues<LeadStatus>().Any(value =>
                    string.Equals(value.ToDbString(), status, StringComparison.OrdinalIgnoreCase)))
                .WithMessage("Status must be Pending, Completed, or Failed.");
        });
    }
}
