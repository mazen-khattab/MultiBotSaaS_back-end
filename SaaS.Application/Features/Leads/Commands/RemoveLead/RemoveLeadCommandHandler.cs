using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.Leads.Commands.RemoveLead;

public class RemoveLeadCommandHandler : IRequestHandler<RemoveLeadCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _context;
    private readonly ILogger<RemoveLeadCommandHandler> _logger;

    public RemoveLeadCommandHandler(IAppDbContext context, ILogger<RemoveLeadCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> Handle(RemoveLeadCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Removing LeadId {LeadId} for UserId {UserId}", request.LeadId, request.UserId);

        // Keep ownership and the existing bot-access rule in the DELETE itself.
        // The database FK deletes LeadDetails atomically, without loading its JSON.
        var removed = await _context.Leads
            .Where(lead => lead.Id == request.LeadId && lead.UserId == request.UserId &&
                (lead.BotId == null || _context.UserBots.Any(assignment =>
                    assignment.UserId == request.UserId && assignment.BotId == lead.BotId)))
            .ExecuteDeleteAsync(cancellationToken);

        if (removed == 0)
        {
            _logger.LogWarning("Lead removal unavailable for LeadId {LeadId} for UserId {UserId}: not found or inaccessible",
                request.LeadId, request.UserId);
            return ApiResponse<bool>.Failure("Lead not found.", ErrorType.NotFound);
        }

        _logger.LogInformation("Removed LeadId {LeadId} and its details for UserId {UserId}", request.LeadId, request.UserId);
        return ApiResponse<bool>.Success(true, "Lead removed successfully.");
    }
}
