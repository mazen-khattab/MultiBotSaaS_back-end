using FluentValidation;

namespace SaaS.Application.Features.Worker.Commands.ProcessJobTimeouts
{
    public class ProcessJobTimeoutsCommandValidator : AbstractValidator<ProcessJobTimeoutsCommand>
    {
        public ProcessJobTimeoutsCommandValidator()
        {
        }
    }
}
