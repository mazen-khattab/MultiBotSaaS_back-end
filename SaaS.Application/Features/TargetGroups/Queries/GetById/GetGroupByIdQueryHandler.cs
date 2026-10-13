using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;

namespace SaaS.Application.Features.TargetGroups.Queries.GetById
{
    public class GetGroupByIdQueryHandler : IRequestHandler<GetGroupByIdQuery, ApiResponse<GroupDetailsDto>>
    {
        private readonly IAppDbContext _context;

        public GetGroupByIdQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<GroupDetailsDto>> Handle(GetGroupByIdQuery request, CancellationToken cancellationToken)
        {
            var result = await _context.TargetGroups
                .Where(x => x.UserId == request.UserId && x.BotId == request.BotId && x.Id == request.Id)
                .Select(x => new
                {
                    Group = x,
                    LeadsCount = x.Leads.Count(),
                    ScrapesCount = x.Scrapes.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result == null)
            {
                return ApiResponse<GroupDetailsDto>.Failure("Target group not found", Domain.Enums.ErrorType.NotFound);
            }

            var groupDto = result.Group.ToDetailsDto(result.LeadsCount, result.ScrapesCount);

            return ApiResponse<GroupDetailsDto>.Success(groupDto, "Target group retrieved successfully");
        }
    }
}
