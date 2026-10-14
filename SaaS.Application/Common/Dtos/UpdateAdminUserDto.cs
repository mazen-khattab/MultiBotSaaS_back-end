using System.Text.Json.Serialization;

namespace SaaS.Application.Common.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateAdminUserDto(string FullName, string Email, string PhoneNumber = "");
