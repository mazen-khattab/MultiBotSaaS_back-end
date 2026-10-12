using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Bots.Queries.GetAll;
using SaaS.Application.Features.Bots.Queries.GetById;
using SaaS.Application.Features.Bots.Queries.GetSubscribed;

namespace SaaS.Api.Controllers.v1
{
    [ApiController]
    [Route("api/v1/bots")]
    [Authorize]
    public class BotsController : ControllerBase
    {
        private readonly ISender _sender;

        public BotsController(ISender sender)
        {
            _sender = sender;
        }

        private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

        /// <summary>
        /// Get all available bots in the platform with subscription status for current user.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<BotDto>>> GetAll()
        {
            var userId = GetUserId();
            var query = new GetAllBotsQuery(userId);
            var result = await _sender.Send(query);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result?.ErrorType.ToHttpStatusCode() ?? StatusCodes.Status500InternalServerError;

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
        /// Get all bots currently subscribed/owned by the authenticated user.
        /// </summary>
        [HttpGet("subscribed")]
        public async Task<ActionResult<List<BotDto>>> GetSubscribed()
        {
            var userId = GetUserId();
            var query = new GetSubscribedBotsQuery(userId);
            var result = await _sender.Send(query);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result?.ErrorType.ToHttpStatusCode() ?? StatusCodes.Status500InternalServerError;

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
        /// Get a specific bot by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<BotDto>> GetById(int id, [FromQuery] bool requireOwnership = false)
        {
            var userId = GetUserId();
            var query = new GetBotByIdQuery(userId, id, requireOwnership);
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
