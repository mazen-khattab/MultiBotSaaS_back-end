using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Api.Filters;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.TargetGroups.Commands.Add;
using SaaS.Application.Features.TargetGroups.Commands.Delete;
using SaaS.Application.Features.TargetGroups.Commands.Update;
using SaaS.Application.Features.TargetGroups.Commands.UpdateCursor;
using SaaS.Application.Features.TargetGroups.Queries.GetAll;
using SaaS.Application.Features.TargetGroups.Queries.GetById;
using System.Security.Claims;

namespace SaaS.Api.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TargetGroupsController : ControllerBase
    {
        private readonly ISender _sender;

        public TargetGroupsController(ISender sender)
        {
            _sender = sender;
        }

        private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

        /// <summary>
        /// Create a new group.
        /// Maps the request to AddGroupCommand and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<object>> Create([FromBody] AddGroupDto requestDto)
        {
            var userId = GetUserId();

            var command = new AddGroupCommand(userId, requestDto);
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
        /// Get all groups for the current user and specified bot.
        /// Maps to GetAllGroupsQuery and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpGet("{botId}")]
        [Authorize]
        public async Task<ActionResult<object>> GetAll([FromRoute] int botId)
        {
            var userId = GetUserId();

            var query = new GetAllGroupsQuery(userId, botId);
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
        /// Get a group by its id for the specified bot.
        /// Maps to GetGroupByIdQuery and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpGet("{botId}/{id}")]
        [Authorize]
        public async Task<ActionResult<object>> GetById([FromRoute] int botId, [FromRoute] int id)
        {
            var userId = GetUserId();

            var query = new GetGroupByIdQuery(userId, botId, id);
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
        /// Update an existing group.
        /// Maps the request to UpdateGroupCommand and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<object>> Update(int id, [FromBody] UpdateGroupDto requestDto)
        {
            var command = new UpdateGroupCommand(GetUserId(), id, requestDto);
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

        [HttpPut("{id}/cursor")]
        [N8nWebhookAuthorize]
        public async Task<ActionResult<object>> UpdateCursor(int id, [FromBody] UpdateGroupCursorDto requestDto)
        {
            var command = new UpdateGroupCursorCommand(id, requestDto.Cursor);
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
        /// Delete a group by its id.
        /// Maps to DeleteGroupCommand and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult<object>> Delete(int id)
        {
            var command = new DeleteGroupCommand(GetUserId(), id);
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
