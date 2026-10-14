using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Leads.Queries.GetLeadById
{
    public record GetLeadByIdQuery(Guid UserId, long LeadId) : IRequest<ApiResponse<LeadDetailsDto>>;
}
