using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Helpers;
using SaaS.Application.Common.Interfaces;
using SaaS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Mapper
{
    public static class UserSettingsMapperExtension
    {
        public static UserSettingsDto ToDto(this UserSetting settings, IEncryptionService encryptionService)
        {
            if (settings == null)
            {
                return new UserSettingsDto(false, string.Empty, false, string.Empty, 0, null, null);
            }

            var hasAiKey = !string.IsNullOrWhiteSpace(settings.AIApiKeyEncrypted);
            var hasScraperToken = !string.IsNullOrWhiteSpace(settings.ScraperApiTokenEncrypted);

            return new UserSettingsDto(
                HasAIApiKey: hasAiKey,
                MaskedAIApiKey: Helper.MaskValue(settings.AIApiKeyEncrypted, encryptionService),
                HasScraperToken: hasScraperToken,
                MaskedScraperToken: Helper.MaskValue(settings.ScraperApiTokenEncrypted, encryptionService),
                DailyLeadLimit: settings.DailyMessageLimit,
                CompanyName: settings.CompanyName,
                CompanyPitch: settings.CompanyPitch
            );
        }
    }
}
