using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminUsers.Commands.CreateUser;

public sealed record CreateAdminUserCommand(CreateAdminUserDto User) : IRequest<ApiResponse<AdminUserDto>>;
