using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.ConnectedAccounts.Commands;
using SaaS.Application.Features.ConnectedAccounts.Commands.Add;
using SaaS.Application.Features.ConnectedAccounts.Commands.Delete;
using SaaS.Application.Features.ConnectedAccounts.Commands.Update;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetAll;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetById;
using SaaS.Application.Features.ConnectedAccounts.Queries.GetEligible;
using SaaS.Domain.Entities;
using SaaS.Domain.Enums;
using System.Security.Claims;

namespace SaaS.Api.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [Authorize]
    [ApiController]
    public class ConnectedAccountsController : ControllerBase
    {
        private readonly ISender _sender;

        public ConnectedAccountsController(ISender sender)
        {
            _sender = sender;
        }

        private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

        /// <summary>
        /// Create a new connected account.
        /// Type must be "Scraping" or "Messaging".
        /// Maps the request to CreateConnectedAccountCommand and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<object>> Create([FromBody] AddConnectedAccountDto requestDto)
        {
            var userId = GetUserId();

            var command = new AddConnectedAccountCommand(userId, requestDto);
            var result = await _sender.Send(command);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result!.ErrorType.ToHttpStatusCode();

            var errorResponse = new ApiErrorResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = result?.Message ?? "Failure during operation.",
                Path = HttpContext.Request.Path,
                Method = HttpContext.Request.Method,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Get eligible active scraping accounts for a specific bot.
        /// </summary>
        [HttpGet("eligible/scraping/{botId}")]
        public Task<ActionResult<object>> GetEligible([FromRoute] int botId) => GetEligibleByType(botId, AccountType.SCRAPING);

        /// <summary>
        /// Get eligible active messaging accounts for a specific bot.
        /// </summary>
        [HttpGet("eligible/messaging/{botId}")]
        public Task<ActionResult<object>> GetEligibleMessaging([FromRoute] int botId) => GetEligibleByType(botId, AccountType.MESSAGING);


        /// <summary>
        /// Get all connected accounts.
        /// Each account includes its Type ("Scraping" or "Messaging").
        /// Maps to GetAllConnectedAccountsQuery and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;IEnumerable&lt;T&gt;&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpGet("{botId}")]
        public async Task<ActionResult<object>> GetAll([FromRoute] int botId)
        {
            var userId = GetUserId();

            var query = new GetAllConnectedAccountsQuery(userId, botId);
            var result = await _sender.Send(query);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result!.ErrorType.ToHttpStatusCode();

            var errorResponse = new ApiErrorResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = result?.Message ?? "Failure during operation.",
                Path = HttpContext.Request.Path,
                Method = HttpContext.Request.Method,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Get connected account details by id.
        /// The returned details include Type ("Scraping" or "Messaging").
        /// Maps to GetConnectedAccountByIdQuery and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpGet("{botId}/{id}")]
        public async Task<ActionResult<object>> GetById([FromRoute] int botId, [FromRoute] int id)
        {
            var userId = GetUserId();

            var query = new GetConnectedAccountByIdQuery(userId, botId, id);
            var result = await _sender.Send(query);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result!.ErrorType.ToHttpStatusCode();

            var errorResponse = new ApiErrorResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = result?.Message ?? "Failure during operation.",
                Path = HttpContext.Request.Path,
                Method = HttpContext.Request.Method,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Update a connected account by id.
        /// Type must be "Scraping" or "Messaging".
        /// Maps the request to UpdateConnectedAccountCommand and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<object>> Update(int id, [FromBody] UpdateConnectedAccountDto requestDto)
        {
            var userId = GetUserId();

            var command = new UpdateConnectedAccountCommand(userId, id, requestDto);
            var result = await _sender.Send(command);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result!.ErrorType.ToHttpStatusCode();

            var errorResponse = new ApiErrorResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = result?.Message ?? "Failure during operation.",
                Path = HttpContext.Request.Path,
                Method = HttpContext.Request.Method,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Delete a connected account by id.
        /// Maps to DeleteConnectedAccountCommand and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<object>> Delete(int id)
        {
            var userId = GetUserId();

            var command = new DeleteConnectedAccountCommand(userId, id);
            var result = await _sender.Send(command);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result!.ErrorType.ToHttpStatusCode();

            var errorResponse = new ApiErrorResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = result?.Message ?? "Failure during operation.",
                Path = HttpContext.Request.Path,
                Method = HttpContext.Request.Method,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        private async Task<ActionResult<object>> GetEligibleByType(int botId, AccountType type)
        {
            var userId = GetUserId();

            var query = new GetEligibleConnectedAccountsQuery(userId, botId, type);
            var result = await _sender.Send(query);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result?.ErrorType.ToHttpStatusCode() ?? StatusCodes.Status404NotFound;

            var errorResponse = new ApiErrorResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = result?.Message ?? "Failure during operation.",
                Path = HttpContext.Request.Path,
                Method = HttpContext.Request.Method,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }
    }
}
