using System.Text.Json.Serialization;

namespace SaaS.Application.Common.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAdminBotDto(string Name, string UiModuleCode,
    [property: JsonRequired] decimal CurrentPrice, string? Description = null, int CooldownMinutes = 180);
