using System.Collections.Generic;

namespace SaaS.Application.Common.Dtos
{
    public record CompleteScrapeDto(List<ScrapedLeadDto> ExtractedLeads);
}
