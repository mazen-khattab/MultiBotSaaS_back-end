using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Dtos
{
    public record GroupDetailsDto(int Id, string GroupName, string GroupUrl, string ConfigJson, bool IsActive, int RelatedLeadsCount, int ScrapesCount);
}
