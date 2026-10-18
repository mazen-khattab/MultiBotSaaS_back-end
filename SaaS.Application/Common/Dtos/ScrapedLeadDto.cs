using System;

namespace SaaS.Application.Common.Dtos
{
    public record ScrapedLeadDto(string ExternalId, string ProfileUrl, string ProfileName, string AiMessage, string MetadataJson);
}
