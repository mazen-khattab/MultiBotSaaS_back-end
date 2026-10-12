using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.Auth.Queries.User.GetCurrentUser
{
    public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, ApiResponse<AuthLoginResponseDto>>
    {
        private readonly IAppDbContext _context;

        public GetCurrentUserQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<AuthLoginResponseDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);

            if (user == null)
            {
                return ApiResponse<AuthLoginResponseDto>.Failure("User not found or inactive.", ErrorType.Unauthorized);
            }

            var userDto = new AuthLoginResponseDto(user.Id.ToString(), user.Email, user.FullName, "User");
            return ApiResponse<AuthLoginResponseDto>.Success(userDto, "Current user retrieved successfully.");
        }
    }
}
