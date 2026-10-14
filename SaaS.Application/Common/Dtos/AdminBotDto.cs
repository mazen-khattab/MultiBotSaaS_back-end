namespace SaaS.Application.Common.Dtos;

public sealed record AdminBotDto(int Id, string Name, string? Description, decimal CurrentPrice,
    string UiModuleCode, int CooldownMinutes, bool IsActive, string Status);
