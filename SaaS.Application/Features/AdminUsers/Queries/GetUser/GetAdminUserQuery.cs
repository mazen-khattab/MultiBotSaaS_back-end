using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminUsers.Queries.GetUser;

public sealed record GetAdminUserQuery(Guid UserId) : IRequest<ApiResponse<AdminUserDto>>;
