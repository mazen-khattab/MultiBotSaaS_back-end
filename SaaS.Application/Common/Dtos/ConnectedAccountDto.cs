using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Dtos
{
    public record ConnectedAccountDto(int Id, string DisplayName, string Platform, DateTime ExpiredAt, bool IsActive, string Type);
}
