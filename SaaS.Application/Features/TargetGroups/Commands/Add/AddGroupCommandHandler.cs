using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.TargetGroups.Commands.Add
{
    public class AddGroupCommandHandler : IRequestHandler<AddGroupCommand, ApiResponse<int>>
    {
        private readonly IAppDbContext _context;
        private readonly IUserBotService _userBotService;

        public AddGroupCommandHandler(IAppDbContext context, IUserBotService userBotService) 
            => (_context, _userBotService) = (context, userBotService);

        public async Task<ApiResponse<int>> Handle(AddGroupCommand request, CancellationToken cancellationToken)
        {
            var dto = request.GroupDto;

            var hasBot = await _userBotService.CheckOwnershipAsync(request.UserId, dto.BotId, cancellationToken);

            if (!hasBot)
            {
                return ApiResponse<int>.Failure("User or bot not found", ErrorType.NotFound);
            }

            var entity = new TargetGroup
            {
                UserId = request.UserId,
                BotId = dto.BotId,
                GroupName = dto.GroupName,
                GroupUrl = dto.GroupUrl,
                ConfigJson = string.IsNullOrWhiteSpace(dto.ConfigJson) ? "{}" : dto.ConfigJson,
                IsActive = true
            };

            await _context.TargetGroups.AddAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<int>.Success(entity.Id, "Target group has been added successfully");
        }
    }
}
