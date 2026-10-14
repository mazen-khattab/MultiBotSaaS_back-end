using System.Linq.Expressions;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Application.Mapper;

public static class AdminUserMapperExtension
{
    // Select only public profile fields, rather than loading security columns for reads.
    public static readonly Expression<Func<User, AdminUserDto>> Projection = user => new AdminUserDto(
        Id: user.Id,
        FullName: user.FullName,
        Email: user.Email,
        PhoneNumber: user.PhoneNumber,
        IsDeleted: user.IsDeleted,
        CreatedAt: user.CreatedAt
    );

    public static AdminUserDto ToAdminDto(this User user)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));

        return new AdminUserDto(
            Id: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            PhoneNumber: user.PhoneNumber,
            IsDeleted: user.IsDeleted,
            CreatedAt: user.CreatedAt
        );
    }

    public static List<AdminUserDto> ToAdminDtoList(this IEnumerable<User> users)
    {
        ArgumentNullException.ThrowIfNull(users, nameof(users));

        return [.. users.Select(u => u.ToAdminDto())];
    }
}
