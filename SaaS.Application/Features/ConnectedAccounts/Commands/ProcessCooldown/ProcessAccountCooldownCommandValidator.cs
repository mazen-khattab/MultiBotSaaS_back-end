using FluentValidation;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.ProcessCooldown
{
    public class ProcessAccountCooldownCommandValidator : AbstractValidator<ProcessAccountCooldownCommand>
    {
        public ProcessAccountCooldownCommandValidator()
        {
        }
    }
}
