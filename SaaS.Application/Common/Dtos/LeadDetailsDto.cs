namespace SaaS.Application.Common.Dtos
{
    public record LeadDetailsDto(
        long Id,
        int? BotId,
        int? GroupId,
        int? AccountId,
        int? ScrapeId,
        string ExternalId,
        string ProfileName,
        string ProfileUrl,
        string? AiMessage,
        string Status,
        DateTime CreatedAt,
        DateTime? ProcessedAt,
        string? MetadataJson);
}
