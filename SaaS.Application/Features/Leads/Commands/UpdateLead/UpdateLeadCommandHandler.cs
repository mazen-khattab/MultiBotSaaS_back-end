using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;
using SaaS.Domain.Extensions;

namespace SaaS.Application.Features.Leads.Commands.UpdateLead;

public class UpdateLeadCommandHandler : IRequestHandler<UpdateLeadCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _context;
    private readonly ILogger<UpdateLeadCommandHandler> _logger;

    public UpdateLeadCommandHandler(IAppDbContext context, ILogger<UpdateLeadCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> Handle(UpdateLeadCommand request, CancellationToken cancellationToken)
    {
        // Log identifiers and outcomes without exposing profile content or AI messages.
        _logger.LogInformation("Updating LeadId {LeadId} for UserId {UserId}", request.LeadId, request.UserId);
        var dto = request.LeadDto;
        var status = Enum.Parse<LeadStatus>(dto.Status, ignoreCase: true).ToDbString();

        // Check ownership in the UPDATE itself so another user's lead cannot be changed.
        // Preserve the same bot-access rule used by the details and removal endpoints.
        var updated = await _context.Leads
            .Where(lead => lead.Id == request.LeadId && lead.UserId == request.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(lead => lead.ProfileName, dto.ProfileName)
                .SetProperty(lead => lead.ProfileUrl, dto.ProfileUrl)
                .SetProperty(lead => lead.AiMessage, dto.AiMessage)
                .SetProperty(lead => lead.Status, status), cancellationToken);

        // Use the same response for missing and inaccessible leads to avoid revealing ownership.
        if (updated == 0)
        {
            _logger.LogWarning("Lead update unavailable for LeadId {LeadId} for UserId {UserId}: not found or inaccessible",
                request.LeadId, request.UserId);
            return ApiResponse<bool>.Failure("Lead not found.", ErrorType.NotFound);
        }

        // Only Leads is updated; no LeadDetails row is loaded, created or modified.
        _logger.LogInformation("Updated LeadId {LeadId} for UserId {UserId}", request.LeadId, request.UserId);
        return ApiResponse<bool>.Success(true, "Lead updated successfully.");
    }
}
