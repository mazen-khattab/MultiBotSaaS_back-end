using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Infrastructure.Services
{
    public class AdminUserPersistenceService : IAdminUserPersistenceService
    {
        private readonly IAppDbContext _db;
        public AdminUserPersistenceService(IAppDbContext db)
        {
            _db = db;
        }
        public Task<bool> EmailExists(string email, Guid? exceptUserId, CancellationToken cancellationToken)
        {
            // Deleted accounts still reserve their email under the existing unique index.
            var users = _db.Users
                .IgnoreQueryFilters()
                .Where(user => user.Email == email);

            if (exceptUserId.HasValue)
            {
                users = users.Where(user => user.Id != exceptUserId.Value);
            }

            return users.AnyAsync(cancellationToken);
        }

        public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // The database unique index also handles two admins claiming an email concurrently.
                return false;
            }
        }
    }
}
