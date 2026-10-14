using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;

namespace SaaS.Infrastructure.Services
{
    public class AdminBotPersistenceService : IAdminBotPersistenceService
    {
        private readonly IAppDbContext _db;

        public AdminBotPersistenceService(IAppDbContext db)
        {
            _db = db;
        }

        public Task<bool> CodeExists(string normalizedCode, CancellationToken cancellationToken) =>
            _db.Bots.AnyAsync(bot => bot.UiModuleCode.Trim() == normalizedCode, cancellationToken);

        public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // The unique database index protects concurrent creates as well as the precheck.
                return false;
            }
        }
    }
}
