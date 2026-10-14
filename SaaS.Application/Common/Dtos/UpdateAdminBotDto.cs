using System.Text.Json.Serialization;

namespace SaaS.Application.Common.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateAdminBotDto(string Name, [property: JsonRequired] decimal CurrentPrice,
    string? Description = null, int CooldownMinutes = 180, string? UiModuleCode = null);
