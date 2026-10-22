using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Leads.Commands.UpdateLead;

// UserId comes from the authenticated identity, never from the request body.
public record UpdateLeadCommand(Guid UserId, long LeadId, UpdateLeadDto LeadDto) : IRequest<ApiResponse<bool>>;
