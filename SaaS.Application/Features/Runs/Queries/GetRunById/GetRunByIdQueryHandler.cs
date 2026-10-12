using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.Runs.Queries.GetRunById
{
    public class GetRunByIdQueryHandler : IRequestHandler<GetRunByIdQuery, ApiResponse<RunDto>>
    {
        private readonly IAppDbContext _context;

        public GetRunByIdQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<RunDto>> Handle(GetRunByIdQuery request, CancellationToken cancellationToken)
        {
            var run = await _context.Runs
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == request.RunId && r.UserId == request.UserId, cancellationToken);

            if (run == null)
            {
                return ApiResponse<RunDto>.Failure("Run not found.", ErrorType.NotFound);
            }

            return ApiResponse<RunDto>.Success(run.ToDto(), "Run retrieved successfully.");
        }
    }
}
