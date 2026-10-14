using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminBots.Commands.SetBotStatus;

public sealed record SetAdminBotStatusCommand(int BotId, bool IsActive) : IRequest<ApiResponse<AdminBotDto>>;
