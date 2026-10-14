namespace SaaS.Application.Common.Dtos;

public sealed record AdminSubscriptionDto(int Id, Guid UserId, string UserFullName, string UserEmail,
    int BotId, string BotName, string UiModuleCode, decimal PurchasePrice, DateTime PurchaseDate, bool IsActive);
