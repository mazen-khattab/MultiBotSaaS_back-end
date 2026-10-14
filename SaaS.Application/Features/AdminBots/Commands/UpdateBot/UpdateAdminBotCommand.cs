using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminBots.Commands.UpdateBot;

public sealed record UpdateAdminBotCommand(int BotId, UpdateAdminBotDto Bot) : IRequest<ApiResponse<AdminBotDto>>;
