using System.Text.Json.Serialization;

namespace SaaS.Application.Common.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAdminUserDto(string FullName, string Email, string Password, string PhoneNumber = "");
