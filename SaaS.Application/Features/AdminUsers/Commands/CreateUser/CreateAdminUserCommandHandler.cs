using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.AdminUsers.Commands.CreateUser;

public sealed class CreateAdminUserCommandHandler(IAppDbContext db, IPasswordHasherService passwordHasher, IAdminUserPersistenceService _adminUserPersistenceService) : IRequestHandler<CreateAdminUserCommand, ApiResponse<AdminUserDto>>
{
    public async Task<ApiResponse<AdminUserDto>> Handle(CreateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.User.Email.Trim();
        if (await _adminUserPersistenceService.EmailExists(email, null, cancellationToken))
        {
            return ApiResponse<AdminUserDto>.Failure("Email is already in use.", ErrorType.ConcurrencyConflict);
        }
        
        var user = new User
        {
            FullName = request.User.FullName.Trim(), Email = email, PhoneNumber = request.User.PhoneNumber.Trim(),
            PasswordHash = passwordHasher.HashPassword(request.User.Password)
        };

        db.Users.Add(user);
        if (!await _adminUserPersistenceService.TrySaveChangesAsync(cancellationToken))
        {
            return ApiResponse<AdminUserDto>.Failure("Email is already in use.", ErrorType.ConcurrencyConflict);
        }

        return ApiResponse<AdminUserDto>.Success(user.ToAdminDto(), "User created successfully.");
    }
}
