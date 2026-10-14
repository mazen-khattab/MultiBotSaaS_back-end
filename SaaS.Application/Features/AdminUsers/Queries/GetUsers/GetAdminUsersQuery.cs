using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminUsers.Queries.GetUsers;

public sealed record GetAdminUsersQuery(string? Search = null, bool? IsDeleted = null,
    int PageNumber = 1, int PageSize = 10) : IRequest<ApiResponse<PaginatedResult<AdminUserDto>>>;
