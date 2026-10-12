using System;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin
{
    public record GetCurrentAdminQuery(Guid AdminId) : IRequest<ApiResponse<AuthLoginResponseDto>>;
}
