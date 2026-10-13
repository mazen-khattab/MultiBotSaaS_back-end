using MediatR;
using SaaS.Application.Common.Models;
using System;

namespace SaaS.Application.Features.Settings.Commands.Manage
{
    public record ManageUserSettingsCommand(
        Guid UserId,
        string? AIApiKey,
        string? ScraperToken,
        int DailyLeadLimit,
        string? CompanyName = null,
        string? CompanyPitch = null) : IRequest<ApiResponse<Guid>>;
}
