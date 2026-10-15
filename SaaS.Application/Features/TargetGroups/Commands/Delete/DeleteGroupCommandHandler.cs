using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;

namespace SaaS.Application.Features.TargetGroups.Commands.Delete
{
    public class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, ApiResponse<bool>>
    {
        private readonly IAppDbContext _context;
        private readonly IUserBotService _userBotService;

        public DeleteGroupCommandHandler(IAppDbContext context, IUserBotService userBotService)
        {
            _context = context;
            _userBotService = userBotService;
        }

        public async Task<ApiResponse<bool>> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
        {
            var hasOwnership = await _userBotService.CheckOwnershipAsync(request.UserId, request.Id, cancellationToken);

            if (!hasOwnership)
            {
                return ApiResponse<bool>.Failure("User or bot not found.", ErrorType.Unauthorized);
            }

            var entity = await _context.TargetGroups.FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == request.UserId, cancellationToken);
            if (entity == null)
            {
                return ApiResponse<bool>.Failure("Target group not found", ErrorType.NotFound);
            }

            _context.TargetGroups.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, "Target group deleted successfully");
        }
    }
}
