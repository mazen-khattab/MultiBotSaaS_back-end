using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Dtos
{
    public record UserSettingsDto(
        bool HasAIApiKey,
        string MaskedAIApiKey,
        bool HasScraperToken,
        string MaskedScraperToken,
        int DailyLeadLimit,
        string? CompanyName = null,
        string? CompanyPitch = null);
}
