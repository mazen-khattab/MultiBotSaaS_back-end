using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminUsers.Commands.SetUserStatus;

public sealed class SetAdminUserStatusCommandHandler(IAppDbContext db)
    : IRequestHandler<SetAdminUserStatusCommand, ApiResponse<AdminUserDto>>
{
    public async Task<ApiResponse<AdminUserDto>> Handle(SetAdminUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(user => user.RefreshTokens)
            .SingleOrDefaultAsync(user => user.Id == request.UserId, cancellationToken);
        if (user is null)
            return ApiResponse<AdminUserDto>.Failure("User not found.", ErrorType.NotFound);

        // A single SaveChanges commits the status and revocations together. Reactivation
        // must not revive old tokens; an already-active user retains their current session.
        if (request.IsDeleted || user.IsDeleted)
        {
            user.CurrentSessionToken = null;
            var now = DateTime.UtcNow;
            foreach (var token in user.RefreshTokens.Where(token => token.IsActive))
            {
                token.IsActive = false;
                token.ExpDate = now;
            }
        }
        user.IsDeleted = request.IsDeleted;
        await db.SaveChangesAsync(cancellationToken);
        return ApiResponse<AdminUserDto>.Success(user.ToAdminDto(), request.IsDeleted
            ? "User deactivated successfully." : "User reactivated successfully.");
    }
}
