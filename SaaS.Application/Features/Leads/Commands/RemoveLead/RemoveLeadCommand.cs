using MediatR;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Leads.Commands.RemoveLead;

public record RemoveLeadCommand(Guid UserId, long LeadId) : IRequest<ApiResponse<bool>>;
