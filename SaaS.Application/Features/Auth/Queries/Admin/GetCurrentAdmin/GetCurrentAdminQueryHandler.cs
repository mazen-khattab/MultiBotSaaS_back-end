using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin
{
    public class GetCurrentAdminQueryHandler : IRequestHandler<GetCurrentAdminQuery, ApiResponse<AuthLoginResponseDto>>
    {
        private readonly IAppDbContext _context;

        public GetCurrentAdminQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<AuthLoginResponseDto>> Handle(GetCurrentAdminQuery request, CancellationToken cancellationToken)
        {
            var admin = await _context.SystmeAdmins
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AdminId && a.IsActive, cancellationToken);

            if (admin == null)
            {
                return ApiResponse<AuthLoginResponseDto>.Failure("Admin not found or inactive.", ErrorType.Unauthorized);
            }

            var adminDto = new AuthLoginResponseDto(admin.Id.ToString(), admin.Email, admin.FullName, admin.Role);
            return ApiResponse<AuthLoginResponseDto>.Success(adminDto, "Current admin retrieved successfully.");
        }
    }
}
