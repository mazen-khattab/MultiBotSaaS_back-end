namespace SaaS.Application.Common.Dtos
{
    public record UpdateGroupDto(string GroupName, string GroupUrl, string ConfigJson, bool IsActive);
}
