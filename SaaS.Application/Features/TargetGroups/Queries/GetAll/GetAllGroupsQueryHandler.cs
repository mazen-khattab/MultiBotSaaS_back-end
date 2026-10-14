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
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.TargetGroups.Queries.GetAll
{
    public class GetAllGroupsQueryHandler : IRequestHandler<GetAllGroupsQuery, ApiResponse<List<GroupDto>>>
    {
        private readonly IAppDbContext _context;
        private readonly IUserBotService _userBotService;

        public GetAllGroupsQueryHandler(IAppDbContext context, IUserBotService userBotService)
        {
            _context = context;
            _userBotService = userBotService;
        }

        public async Task<ApiResponse<List<GroupDto>>> Handle(GetAllGroupsQuery request, CancellationToken cancellationToken)
        {
            var hasOwnership = await _userBotService.CheckOwnershipAsync(request.UserId, request.BotId, cancellationToken);

            if(!hasOwnership)
            {
                return ApiResponse<List<GroupDto>>.Failure("User or bot not found", ErrorType.NotFound);
            }

            var groups = await _context.TargetGroups
                .Where(x => x.UserId == request.UserId && x.BotId == request.BotId)
                .ToListAsync(cancellationToken);

            if (groups == null || !groups.Any())
            {
                return ApiResponse<List<GroupDto>>.Success(new List<GroupDto>(), "No target groups found");
            }

            var groupDto = groups.ToDtoList();

            return ApiResponse<List<GroupDto>>.Success(groupDto, "Target groups have been retrieved successfully");
        }
    }
}
