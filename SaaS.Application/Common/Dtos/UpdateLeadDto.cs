namespace SaaS.Application.Common.Dtos;

// Only these lead fields can be updated; ownership, relationships and metadata are excluded.
public record UpdateLeadDto(string ProfileName, string ProfileUrl, string? AiMessage, string Status);
