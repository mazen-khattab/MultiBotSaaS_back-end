using MediatR;
using SaaS.Application.Common.Models;
using System;

namespace SaaS.Application.Features.Worker.Commands.LogBotActivity
{
    public class LogBotActivityCommand : IRequest<ApiResponse<bool>>
    {
        public string CorrelationId { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public int? BotId { get; set; }
        public string LogLevel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
    }
}
