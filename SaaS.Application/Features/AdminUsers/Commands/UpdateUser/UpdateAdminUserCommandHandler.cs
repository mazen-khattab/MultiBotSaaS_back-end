using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminUsers.Commands.UpdateUser;

public sealed class UpdateAdminUserCommandHandler(IAppDbContext _db, IAdminUserPersistenceService _adminUserPersistence) : IRequestHandler<UpdateAdminUserCommand, ApiResponse<AdminUserDto>>
{
    public async Task<ApiResponse<AdminUserDto>> Handle(UpdateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(user => user.Id == request.UserId, cancellationToken);

        if (user is null)
            return ApiResponse<AdminUserDto>.Failure("User not found.", ErrorType.NotFound);

        var newEmail = request.User.Email.Trim();
        var isEmailChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);

        if (isEmailChanged && await _adminUserPersistence.EmailExists(newEmail, user.Id, cancellationToken))
            return ApiResponse<AdminUserDto>.Failure("Email is already in use.", ErrorType.ConcurrencyConflict);

        user.FullName = request.User.FullName.Trim();
        user.Email = newEmail;
        user.PhoneNumber = request.User.PhoneNumber.Trim();

        if (!await _adminUserPersistence.TrySaveChangesAsync(cancellationToken))
            return ApiResponse<AdminUserDto>.Failure("Email is already in use.", ErrorType.ConcurrencyConflict);
        return ApiResponse<AdminUserDto>.Success(user.ToAdminDto(), "User updated successfully.");
    }
}
