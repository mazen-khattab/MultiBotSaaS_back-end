using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.ExceptionTypes;
using SaaS.Domain.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.Update
{
    public class UpdateConnectedAccountCommandHandler : IRequestHandler<UpdateConnectedAccountCommand, ApiResponse<Guid>>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public UpdateConnectedAccountCommandHandler(IAppDbContext context, IEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public async Task<ApiResponse<Guid>> Handle(UpdateConnectedAccountCommand request, CancellationToken cancellationToken)
        {
            var accountInfo = request.AccountDto;

            var entity = await _context.ConnectedAccounts
                .Include(ca => ca.Cookie)
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == request.UserId, cancellationToken);

            if (entity is null)
            {
                return ApiResponse<Guid>.Failure($"Connected account not found.", ErrorType.NotFound);
            }

            entity.DisplayName = accountInfo.DisplayName;
            entity.Platform = accountInfo.Platform;
            entity.IsActive = accountInfo.IsActive;
            entity.Status = accountInfo.Status.ParseFromDbToAccountStatus().ToDbString();

            if (!string.IsNullOrWhiteSpace(accountInfo.CookiesPayload))
            {
                entity.Cookie.EncryptedCookies = _encryptionService.Encrypt(accountInfo.CookiesPayload);
                entity.Cookie.CookiesExpireDate = DateTime.UtcNow.AddDays(7);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<Guid>.Success(entity.UserId, "Connected account has been updated successfully");
        }
    }

}
