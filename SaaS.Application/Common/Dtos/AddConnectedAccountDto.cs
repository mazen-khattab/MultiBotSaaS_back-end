using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Dtos
{
    public record AddConnectedAccountDto(int BotId, string DisplayName, string Platform, string CookiesPayload, string Type);
}
