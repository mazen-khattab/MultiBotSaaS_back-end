using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.TargetGroups.Commands.Add;
using SaaS.Application.Features.TargetGroups.Commands.Delete;
using SaaS.Application.Features.TargetGroups.Commands.Update;
using SaaS.Application.Features.TargetGroups.Queries.GetAll;
using SaaS.Application.Features.TargetGroups.Queries.GetById;
using System.Security.Claims;

namespace SaaS.Api.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize]
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
        /// Get all groups for the current user.
        /// Maps to GetAllGroupsQuery and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<object>> GetAll()
        {
            var userId = GetUserId();

            var query = new GetAllGroupsQuery(userId);
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
        /// Get a group by its id.
        /// Maps to GetGroupByIdQuery and forwards to MediatR.
        /// Returns Ok(ApiResponse&lt;T&gt;) on success or a mapped ApiErrorResponse on failure.
        /// </summary>

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetById(int id)
        {
            var query = new GetGroupByIdQuery(id);
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
        public async Task<ActionResult<object>> Update(int id, [FromBody] UpdateGroupDto requestDto)
        {
            var command = new UpdateGroupCommand(id, requestDto);
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
        public async Task<ActionResult<object>> Delete(int id)
        {
            var command = new DeleteGroupCommand(id);
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
