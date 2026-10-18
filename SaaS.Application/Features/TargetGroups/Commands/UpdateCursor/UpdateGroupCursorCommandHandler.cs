using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.TargetGroups.Commands.UpdateCursor
{
    public class UpdateGroupCursorCommandHandler : IRequestHandler<UpdateGroupCursorCommand, ApiResponse<bool>>
    {
        private readonly IAppDbContext _context;

        public UpdateGroupCursorCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<bool>> Handle(UpdateGroupCursorCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.TargetGroups.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (entity == null)
            {
                return ApiResponse<bool>.Failure("Target group not found", ErrorType.NotFound);
            }

            entity.LastCursor = request.Cursor;

            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<bool>.Success(true, "Target group updated successfully");
        }
    }
}
