using System;
using MediatR;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;

namespace SaaS.Application.Features.Runs.Queries.GetRunById
{
    public record GetRunByIdQuery(Guid UserId, int RunId) : IRequest<ApiResponse<RunDto>>;
}
