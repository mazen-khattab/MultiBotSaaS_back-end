using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;

namespace SaaS.Application.Features.TargetGroups.Queries.GetAll
{
    public class GetAllGroupsQueryHandler : IRequestHandler<GetAllGroupsQuery, ApiResponse<List<GroupDto>>>
    {
        private readonly IAppDbContext _context;

        public GetAllGroupsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<GroupDto>>> Handle(GetAllGroupsQuery request, CancellationToken cancellationToken)
        {
            var groups = await _context.TargetGroups
                .Where(x => x.UserId == request.UserId && x.BotId == request.BotId)
                .ToListAsync(cancellationToken);

            if (groups == null || !groups.Any())
            {
                return ApiResponse<List<GroupDto>>.Failure("No target groups found for the specified user", Domain.Enums.ErrorType.NotFound);
            }

            var groupDto = groups.ToDtoList();

            return ApiResponse<List<GroupDto>>.Success(groupDto, "Target groups have been retrieved successfully");
        }
    }
}
