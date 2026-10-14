namespace SaaS.Application.Common.Interfaces
{
    public interface IAdminBotPersistenceService
    {
        Task<bool> CodeExists(string normalizedCode, CancellationToken cancellationToken);
        Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
    }
}
