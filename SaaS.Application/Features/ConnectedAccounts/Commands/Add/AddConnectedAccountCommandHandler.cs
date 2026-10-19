using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.Add
{
    public class AddConnectedAccountCommandHandler : IRequestHandler<AddConnectedAccountCommand, ApiResponse<Guid>>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IUserBotService _userBotService;

        public AddConnectedAccountCommandHandler(IAppDbContext context, IEncryptionService encryptionService, IUserBotService userBotService)
        {
            _context = context;
            _encryptionService = encryptionService;
            _userBotService = userBotService;
        }

        public async Task<ApiResponse<Guid>> Handle(AddConnectedAccountCommand request, CancellationToken cancellationToken)
        {
            var encrypted = string.Empty;
            var accountInfo = request.AccountDto;

            if (!string.IsNullOrWhiteSpace(accountInfo.CookiesPayload))
            {
                encrypted = _encryptionService.Encrypt(accountInfo.CookiesPayload);
            }

            var hasBot = await _userBotService.CheckOwnershipAsync(request.UserId, accountInfo.BotId, cancellationToken);

            if (!hasBot)
            {
                return ApiResponse<Guid>.Failure("User or bot not found", ErrorType.NotFound);
            }

            var entity = new ConnectedAccount
            {
                UserId = request.UserId,
                BotId = accountInfo.BotId,
                DisplayName = accountInfo.DisplayName,
                Platform = accountInfo.Platform,
                Type = accountInfo.Type.ParseFromDbToAccountType().ToDbString(),
                IsActive = true,
                Cookie = new ConnectedAccountCookie
                {
                    EncryptedCookies = encrypted,
                    CookiesExpireDate = DateTime.UtcNow.AddDays(7),
                }
            };

            await _context.ConnectedAccounts.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<Guid>.Success(request.UserId, "Connected account has been added successfully");
        }
    }
}
