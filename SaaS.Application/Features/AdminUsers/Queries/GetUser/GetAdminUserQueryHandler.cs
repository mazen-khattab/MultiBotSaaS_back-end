using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminUsers.Queries.GetUser;

public sealed class GetAdminUserQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAdminUserQuery, ApiResponse<AdminUserDto>>
{
    public async Task<ApiResponse<AdminUserDto>> Handle(GetAdminUserQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(user => user.Id == request.UserId)
            .Select(AdminUserMapperExtension.Projection).SingleOrDefaultAsync(cancellationToken);
        return user is null ? ApiResponse<AdminUserDto>.Failure("User not found.", ErrorType.NotFound)
            : ApiResponse<AdminUserDto>.Success(user, "User retrieved successfully.");
    }
}
