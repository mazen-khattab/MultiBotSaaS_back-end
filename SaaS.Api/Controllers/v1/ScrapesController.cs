using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Api.Extensions;
using SaaS.Application.Features.Scrapes.Commands.Create;
using SaaS.Application.Features.Scrapes.Commands.Complete;
using SaaS.Application.Features.Scrapes.Commands.Cancel;
using SaaS.Application.Features.Scrapes.Queries.GetAllScrapes;
using SaaS.Application.Features.Scrapes.Queries.GetScrapeById;
using SaaS.Api.Filters;
using Microsoft.AspNetCore.Authorization;

namespace SaaS.Api.Controllers.v1
{
    [ApiController]
    [Route("api/v1/scrapes")]
    public class ScrapesController : ControllerBase
    {
        private readonly ISender _sender;

        public ScrapesController(ISender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        /// <summary>
        /// Get paginated list of scrapes for the current user, optionally filtered by botId and status.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<object>> GetAll([FromQuery] int botId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? statusFilter = null)
        {
            var userId = GetUserId();
            var query = new GetAllScrapesQuery(userId, botId, pageNumber, pageSize, statusFilter);
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
        /// Get a specific scrape by ID.
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<object>> GetById(int id)
        {
            var userId = GetUserId();
            var query = new GetScrapeByIdQuery(userId, id);
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

        /// <summary>
        /// Cancel a currently running scrape.
        /// </summary>
        [HttpPost("{id}/cancel")]
        [Authorize]
        public async Task<ActionResult<object>> Cancel(int id)
        {
            var userId = GetUserId();
            var command = new CancelScrapeCommand(userId, id);
            var result = await _sender.Send(command);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result?.ErrorType.ToHttpStatusCode() ?? StatusCodes.Status400BadRequest;

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
        /// Called by n8n when a scrape completes scraping.
        /// Protected by X-Webhook-Secret header validated by N8nWebhookAuthorizeFilter.
        /// </summary>
        [HttpPost("{id}/complete")]
        [N8nWebhookAuthorize]
        public async Task<ActionResult<object>> Complete(int id, [FromBody] CompleteScrapeDto request)
        {
            var command = new CompleteScrapeCommand(id, request?.ExtractedLeads ?? []);
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

        private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<object>> Create([FromBody] CreateScrapeDto requestDto)
        {
            var userId = GetUserId();
            var command = new CreateScrapeCommand(userId, requestDto);
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
    }
}
