using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Common.Services
{
    public class SessionTokenValidator : ISessionTokenValidator
    {
        private readonly IAppDbContext _dbContext;

        public SessionTokenValidator(IAppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string?> GetCurrentSessionTokenAsync(Guid userId)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId && !u.IsDeleted)
                .Select(u => u.CurrentSessionToken)
                .FirstOrDefaultAsync();
        }
    }
}
