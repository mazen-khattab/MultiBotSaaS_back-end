using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Dtos
{
    public record ConnectedAccountDetailsDto(int Id, string DisplayName, string Platform, string MaskedCookies, DateTime ExpiredAt, bool IsActive, int RelatedLeadsCount, int RunsCount);
}
