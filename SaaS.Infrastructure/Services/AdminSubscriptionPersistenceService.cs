using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;

namespace SaaS.Infrastructure.Services;

public sealed class AdminSubscriptionPersistenceService(IAppDbContext db) : IAdminSubscriptionPersistenceService
{
    public Task<bool> AssignmentExistsAsync(Guid userId, int botId, CancellationToken cancellationToken) =>
        db.UserBots.AnyAsync(assignment => assignment.UserId == userId && assignment.BotId == botId, cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The unique pair index also rejects two admins assigning the same bot concurrently.
            return false;
        }
    }
}
