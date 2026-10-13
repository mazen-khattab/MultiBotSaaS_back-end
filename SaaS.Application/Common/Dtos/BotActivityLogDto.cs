namespace SaaS.Application.Common.Dtos;

// Explicitly project public fields so internal stack traces never enter the response.
public sealed record BotActivityLogDto(
    long Id,
    string CorrelationId,
    string LogLevel,
    string Message,
    DateTime CreatedAt);
