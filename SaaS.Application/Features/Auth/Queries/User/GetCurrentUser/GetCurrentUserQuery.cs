using System;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Auth.Queries.User.GetCurrentUser
{
    public record GetCurrentUserQuery(Guid UserId) : IRequest<ApiResponse<AuthLoginResponseDto>>;
}
