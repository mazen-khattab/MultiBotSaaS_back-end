using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Api.Extensions;
using SaaS.Application.Features.Runs.Commands.Create;
using SaaS.Application.Features.Runs.Commands.Complete;
using SaaS.Application.Features.Runs.Commands.Cancel;
using SaaS.Application.Features.Runs.Queries.GetAllRuns;
using SaaS.Application.Features.Runs.Queries.GetRunById;
using SaaS.Api.Filters;
using Microsoft.AspNetCore.Authorization;

namespace SaaS.Api.Controllers.v1
{
    [ApiController]
    [Route("api/v1/runs")]
    public class RunsController : ControllerBase
    {
        private readonly ISender _sender;

        public RunsController(ISender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        /// <summary>
        /// Get paginated list of runs for the current user, optionally filtered by botId and status.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<object>> GetAll(
            [FromQuery] int? botId = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? statusFilter = null)
        {
            var userId = GetUserId();
            var query = new GetAllRunsQuery(userId, botId, pageNumber, pageSize, statusFilter);
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
        /// Get a specific run by ID.
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<object>> GetById(int id)
        {
            var userId = GetUserId();
            var query = new GetRunByIdQuery(userId, id);
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
        /// Cancel a currently running run.
        /// </summary>
        [HttpPost("{id}/cancel")]
        [Authorize]
        public async Task<ActionResult<object>> Cancel(int id)
        {
            var userId = GetUserId();
            var command = new CancelRunCommand(userId, id);
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
        /// Called by n8n when a run completes scraping.
        /// Protected by X-Webhook-Secret header validated by N8nWebhookAuthorizeFilter.
        /// </summary>
        [HttpPost("{id}/complete")]
        [N8nWebhookAuthorize]
        public async Task<ActionResult<object>> Complete(int id, [FromBody] CompleteRunDto request)
        {
            var command = new CompleteRunCommand(id, request?.ExtractedLeads ?? []);
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
        public async Task<ActionResult<object>> Create([FromBody] CreateRunDto requestDto)
        {
            var userId = GetUserId();
            var command = new CreateRunCommand(userId, requestDto);
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
