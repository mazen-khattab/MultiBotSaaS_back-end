using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;

namespace SaaS.Application.Features.AdminUsers.Queries.GetUsers;

public sealed class GetAdminUsersQueryHandler(IAppDbContext db)
    : IRequestHandler<GetAdminUsersQuery, ApiResponse<PaginatedResult<AdminUserDto>>>
{
    public async Task<ApiResponse<PaginatedResult<AdminUserDto>>> Handle(GetAdminUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();
        if (request.IsDeleted.HasValue)
            users = users.Where(user => user.IsDeleted == request.IsDeleted.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToUpperInvariant();
            users = users.Where(user => user.FullName.ToUpper().Contains(search) || user.Email.ToUpper().Contains(search));
        }

        var count = await users.CountAsync(cancellationToken);
        var items = await users.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(AdminUserMapperExtension.Projection).ToListAsync(cancellationToken);
        return ApiResponse<PaginatedResult<AdminUserDto>>.Success(new PaginatedResult<AdminUserDto>
        {
            Items = items, TotalItems = count,
            TotalPages = (int)Math.Ceiling(count / (double)request.PageSize), CurrentPage = request.PageNumber
        }, "Users retrieved successfully.");
    }
}
