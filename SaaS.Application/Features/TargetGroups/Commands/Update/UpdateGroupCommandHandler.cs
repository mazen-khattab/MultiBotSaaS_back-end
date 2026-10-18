using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.TargetGroups.Commands.Update
{
    public class UpdateGroupCommandHandler : IRequestHandler<UpdateGroupCommand, ApiResponse<bool>>
    {
        private readonly IAppDbContext _context;

        public UpdateGroupCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<bool>> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.TargetGroups.FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == request.UserId, cancellationToken);

            if (entity == null)
            {
                return ApiResponse<bool>.Failure("Target group not found", ErrorType.NotFound);
            }

            //entity.GroupName = request.GroupDto.GroupName;
            //entity.GroupUrl = request.GroupDto.GroupUrl;
            //entity.ConfigJson = string.IsNullOrWhiteSpace(request.GroupDto.ConfigJson) ? entity.ConfigJson : request.GroupDto.ConfigJson;
            //entity.IsActive = request.GroupDto.IsActive;
            entity.FromDto(request.GroupDto);

            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, "Target group updated successfully");
        }
    }
}
