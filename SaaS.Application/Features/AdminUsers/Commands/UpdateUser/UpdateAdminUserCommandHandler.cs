using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminUsers.Commands.UpdateUser;

public sealed class UpdateAdminUserCommandHandler(IAppDbContext db)
    : IRequestHandler<UpdateAdminUserCommand, ApiResponse<AdminUserDto>>
{
    public async Task<ApiResponse<AdminUserDto>> Handle(UpdateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(user => user.Id == request.UserId, cancellationToken);
        if (user is null)
            return ApiResponse<AdminUserDto>.Failure("User not found.", ErrorType.NotFound);
        var email = request.User.Email.Trim();
        if (await AdminUserPersistence.EmailExists(db, email, user.Id, cancellationToken))
            return ApiResponse<AdminUserDto>.Failure("Email is already in use.", ErrorType.ConcurrencyConflict);

        user.FullName = request.User.FullName.Trim();
        user.Email = email;
        user.PhoneNumber = request.User.PhoneNumber.Trim();
        if (!await AdminUserPersistence.Save(db, cancellationToken))
            return ApiResponse<AdminUserDto>.Failure("Email is already in use.", ErrorType.ConcurrencyConflict);
        return ApiResponse<AdminUserDto>.Success(user.ToAdminDto(), "User updated successfully.");
    }
}
