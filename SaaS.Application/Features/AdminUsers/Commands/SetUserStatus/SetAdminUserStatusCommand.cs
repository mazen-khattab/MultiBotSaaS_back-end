using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminUsers.Commands.SetUserStatus;

public sealed record SetAdminUserStatusCommand(Guid UserId, bool IsDeleted) : IRequest<ApiResponse<AdminUserDto>>;
