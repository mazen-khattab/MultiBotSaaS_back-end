using System.Text.Json.Serialization;

namespace SaaS.Application.Common.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignAdminSubscriptionDto(Guid UserId, int BotId);
