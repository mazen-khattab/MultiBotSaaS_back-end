namespace SaaS.Application.Common.Interfaces;

public interface IAdminSubscriptionPersistenceService
{
    Task<bool> AssignmentExistsAsync(Guid userId, int botId, CancellationToken cancellationToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
