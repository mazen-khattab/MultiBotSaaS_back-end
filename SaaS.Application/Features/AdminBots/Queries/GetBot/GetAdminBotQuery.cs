using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminBots.Queries.GetBot;

public sealed record GetAdminBotQuery(int BotId) : IRequest<ApiResponse<AdminBotDto>>;
