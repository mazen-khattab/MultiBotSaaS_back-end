using FluentValidation;

namespace SaaS.Application.Features.ActivityLogs.Queries.GetActivityLogs;

public sealed class GetActivityLogsQueryValidator : AbstractValidator<GetActivityLogsQuery>
{
    public GetActivityLogsQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.BotId).GreaterThan(0).When(query => query.BotId.HasValue);
        RuleFor(query => query.RunId).NotEmpty().MaximumLength(100)
            .When(query => query.RunId is not null);
        RuleFor(query => query.LogLevel)
            .Must(level => level is "INFO" or "WARN" or "ERROR")
            .WithMessage("LogLevel must be 'INFO', 'WARN', or 'ERROR'.")
            .When(query => query.LogLevel is not null);
        RuleFor(query => query.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query).Must(query => ((long)query.PageNumber - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested pagination offset is too large.");
    }
}
