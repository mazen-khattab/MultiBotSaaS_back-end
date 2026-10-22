using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.Leads.Queries.GetLeadById
{
    public class GetLeadByIdQueryHandler : IRequestHandler<GetLeadByIdQuery, ApiResponse<LeadDetailsDto>>
    {
        private readonly IAppDbContext _context;
        private readonly IUserBotService _userBotService;
        private readonly ILogger<GetLeadByIdQueryHandler> _logger;

        public GetLeadByIdQueryHandler(IAppDbContext context, IUserBotService userBotService,
            ILogger<GetLeadByIdQueryHandler> logger)
        {
            _context = context;
            _userBotService = userBotService;
            _logger = logger;
        }

        public async Task<ApiResponse<LeadDetailsDto>> Handle(GetLeadByIdQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retrieving details for LeadId {LeadId} for UserId {UserId}", request.LeadId, request.UserId);

            // A bot can be shared by many users; its assignment alone cannot establish lead ownership.
            var lead = await _context.Leads
                .AsNoTracking()
                .Where(candidate => candidate.Id == request.LeadId && candidate.UserId == request.UserId)
                .Select(candidate => new LeadDetailsDto(
                    candidate.Id, candidate.BotId, candidate.GroupId, candidate.AccountId, candidate.ScrapeId,
                    candidate.ExternalId, candidate.ProfileName, candidate.ProfileUrl, candidate.AiMessage,
                    candidate.Status, candidate.CreatedAt, candidate.ProcessedAt,
                    candidate.Detail != null ? candidate.Detail.MetaDataJson : null))
                .SingleOrDefaultAsync(cancellationToken);

            // Nullable bot links use SetNull on deletion; the lead's owner can still read that history.
            if (lead == null || (lead.BotId.HasValue && !await _userBotService.CheckOwnershipAsync(request.UserId, lead.BotId.Value, cancellationToken)))
            {
                _logger.LogWarning("Lead details unavailable for LeadId {LeadId} for UserId {UserId}: not found or inaccessible",
                    request.LeadId, request.UserId);
                return ApiResponse<LeadDetailsDto>.Failure("Lead not found.", ErrorType.NotFound);
            }

            _logger.LogInformation("Retrieved details for LeadId {LeadId} for UserId {UserId}", request.LeadId, request.UserId);
            return ApiResponse<LeadDetailsDto>.Success(lead, "Lead retrieved successfully.");
        }
    }
}
