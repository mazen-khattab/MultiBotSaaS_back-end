using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Interfaces
{
    public interface IAdminUserPersistenceService
    {
        Task<bool> EmailExists(string email, Guid? exceptUserId, CancellationToken cancellationToken);
        Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
    }
}
