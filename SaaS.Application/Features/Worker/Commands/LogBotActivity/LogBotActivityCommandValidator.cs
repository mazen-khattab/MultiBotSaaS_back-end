using FluentValidation;

namespace SaaS.Application.Features.Worker.Commands.LogBotActivity
{
    public class LogBotActivityCommandValidator : AbstractValidator<LogBotActivityCommand>
    {
        public LogBotActivityCommandValidator()
        {
            RuleFor(x => x.BotId).GreaterThan(0).When(x => x.BotId.HasValue);

            RuleFor(x => x.CorrelationId)
                .NotEmpty()
                .WithMessage("CorrelationId is required.");

            RuleFor(x => x.UserId)
                .NotEqual(Guid.Empty)
                .WithMessage("A valid user Id must be provided.");

            RuleFor(x => x.LogLevel)
                .NotEmpty()
                .WithMessage("LogLevel is required.")
                .Must(level => level == "INFO" || level == "WARN" || level == "ERROR")
                .WithMessage("LogLevel must be 'INFO', 'WARN', or 'ERROR'.");

            RuleFor(x => x.Message)
                .NotEmpty()
                .WithMessage("Message is required.");
        }
    }
}
