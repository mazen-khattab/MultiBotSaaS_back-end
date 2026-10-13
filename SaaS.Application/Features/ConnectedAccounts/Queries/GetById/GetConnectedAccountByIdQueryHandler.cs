using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Mapper;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using SaaS.Domain.ExceptionTypes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SaaS.Application.Features.ConnectedAccounts.Queries.GetById
{
    public class GetConnectedAccountByIdQueryHandler : IRequestHandler<GetConnectedAccountByIdQuery, ApiResponse<ConnectedAccountDetailsDto>>
    {
        private readonly IAppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        public GetConnectedAccountByIdQueryHandler(IAppDbContext context, IEncryptionService encryptionService) => (_context, _encryptionService) = (context, encryptionService);

        public async Task<ApiResponse<ConnectedAccountDetailsDto>> Handle(GetConnectedAccountByIdQuery request, CancellationToken cancellationToken)
        {
            var result = await _context.ConnectedAccounts
                .Where(ca => ca.UserId == request.UserId && ca.BotId == request.BotId && ca.Id == request.Id)
                .Select(x => new
                {
                    Account = x,
                    Cookies = x.Cookie,
                    LeadsCount = x.Leads.Count(),
                    RunsCount = x.Runs.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null)
            {
                return ApiResponse<ConnectedAccountDetailsDto>.Failure($"Connected account with ID {request.Id} not found", ErrorType.NotFound);
            }

            var accountDto = result.Account.ToDetailsDto(_encryptionService, result.LeadsCount, result.RunsCount);

            return ApiResponse<ConnectedAccountDetailsDto>.Success(accountDto, "Connected account has been retrieved successfully");
        }
    }
}
