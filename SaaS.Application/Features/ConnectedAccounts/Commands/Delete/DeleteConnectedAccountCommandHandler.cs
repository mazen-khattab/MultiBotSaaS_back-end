using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.ExceptionTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Commands.Delete
{
    public class DeleteConnectedAccountCommandHandler : IRequestHandler<DeleteConnectedAccountCommand, ApiResponse<string>>
    {
        private readonly IAppDbContext _context;

        public DeleteConnectedAccountCommandHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<string>> Handle(DeleteConnectedAccountCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ConnectedAccounts
                .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == request.UserId, cancellationToken);

            if (entity is null)
            {
                return ApiResponse<string>.Failure("Account not found", ErrorType.NotFound);
            }

            _context.ConnectedAccounts.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return ApiResponse<string>.Success(entity.Id.ToString(), "Account has been deleted successfully");
        }
    }
}
