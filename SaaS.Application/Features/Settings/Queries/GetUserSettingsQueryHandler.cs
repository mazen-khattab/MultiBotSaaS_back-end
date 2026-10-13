using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;

namespace SaaS.Application.Features.Settings.Queries
{
    public class GetUserSettingsQueryHandler : IRequestHandler<GetUserSettingsQuery, ApiResponse<UserSettingsDto>>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public GetUserSettingsQueryHandler(IAppDbContext context, IEncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }

        public async Task<ApiResponse<UserSettingsDto>> Handle(GetUserSettingsQuery request, CancellationToken cancellationToken)
        {
            var settings = await _context.UserSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);

            if (settings == null)
            {
                var userSettings = new UserSettingsDto(false, string.Empty, false, string.Empty, 0, null, null);
                return ApiResponse<UserSettingsDto>.Success(userSettings, "No settings found for the user.");
            }

            var settingsDto = settings.ToDto(_encryptionService);

            return ApiResponse<UserSettingsDto>.Success(settingsDto, "Settings retrieved successfully.");
        }
    }
}
