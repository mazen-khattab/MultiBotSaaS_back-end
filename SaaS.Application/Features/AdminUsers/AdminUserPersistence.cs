using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;

namespace SaaS.Application.Features.AdminUsers;

internal static class AdminUserPersistence
{
    public static Task<bool> EmailExists(IAppDbContext db, string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();
        // Deleted accounts still reserve their email under the existing unique index.
        var users = db.Users.Where(user => user.Email.ToUpper() == normalizedEmail);
        if (exceptUserId.HasValue)
            users = users.Where(user => user.Id != exceptUserId.Value);
        return users.AnyAsync(cancellationToken);
    }

    public static async Task<bool> Save(IAppDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The database unique index also handles two admins claiming an email concurrently.
            return false;
        }
    }
}
