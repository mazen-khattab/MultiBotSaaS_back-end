using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.AdminBots.Commands.CreateBot;

public sealed record CreateAdminBotCommand(CreateAdminBotDto Bot) : IRequest<ApiResponse<AdminBotDto>>;
