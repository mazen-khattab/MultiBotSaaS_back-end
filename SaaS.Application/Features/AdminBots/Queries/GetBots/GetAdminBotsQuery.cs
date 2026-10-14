using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminBots.Queries.GetBots;

public sealed record GetAdminBotsQuery(string? Search = null, bool? IsActive = null,
    int PageNumber = 1, int PageSize = 10) : IRequest<ApiResponse<PaginatedResult<AdminBotDto>>>;
