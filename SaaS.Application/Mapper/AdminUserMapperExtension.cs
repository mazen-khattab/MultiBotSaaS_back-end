using System.Linq.Expressions;
using SaaS.Application.Common.Dtos;
using SaaS.Domain.Entities;

namespace SaaS.Application.Mapper;

public static class AdminUserMapperExtension
{
    // Select only public profile fields, rather than loading security columns for reads.
    public static readonly Expression<Func<User, AdminUserDto>> Projection = user => new AdminUserDto(
        user.Id, user.FullName, user.Email, user.PhoneNumber, user.IsDeleted, user.CreatedAt);

    public static AdminUserDto ToAdminDto(this User user) => new(
        user.Id, user.FullName, user.Email, user.PhoneNumber, user.IsDeleted, user.CreatedAt);
}
