using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminUsers.Commands.UpdateUser;

public sealed record UpdateAdminUserCommand(Guid UserId, UpdateAdminUserDto User) : IRequest<ApiResponse<AdminUserDto>>;
