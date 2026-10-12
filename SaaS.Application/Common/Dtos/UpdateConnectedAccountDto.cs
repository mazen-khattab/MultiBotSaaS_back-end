using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Dtos
{
    public record UpdateConnectedAccountDto(string DisplayName, string Platform, string Cookies, string Status, bool IsActive);
}
