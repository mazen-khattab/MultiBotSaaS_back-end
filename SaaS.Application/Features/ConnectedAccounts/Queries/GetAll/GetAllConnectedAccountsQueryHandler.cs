using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetAll
{
    public class GetAllConnectedAccountsQueryHandler : IRequestHandler<GetAllConnectedAccountsQuery, ApiResponse<List<ConnectedAccountDto>>>
    {
        private readonly IAppDbContext _context;

        public GetAllConnectedAccountsQueryHandler(IAppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResponse<List<ConnectedAccountDto>>> Handle(GetAllConnectedAccountsQuery request, CancellationToken cancellationToken)
        {
            var accounts = await _context.ConnectedAccounts
                .Include(ca => ca.Cookie)
                .Where(x => x.UserId == request.UserId && x.BotId == request.BotId)
                .ToListAsync(cancellationToken);

            if (accounts == null || !accounts.Any())
            {
                return ApiResponse<List<ConnectedAccountDto>>.Failure("No connected accounts found for the specified user", ErrorType.NotFound);
            }

            var accountDtos = accounts.ToDtoList();

            return ApiResponse<List<ConnectedAccountDto>>.Success(accountDtos, "Connected accounts have been retrieved successfully");
        }

    }
}
