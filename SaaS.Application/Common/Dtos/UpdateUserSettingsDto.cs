using System.Text.Json.Serialization;

namespace SaaS.Application.Common.Dtos;

// Identity and server-managed fields cannot be supplied by the client.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateUserSettingsDto(
    string? AIApiKey,
    string? ScraperToken,
    [property: JsonRequired] int DailyLeadLimit,
    string? CompanyName = null,
    string? CompanyPitch = null);
