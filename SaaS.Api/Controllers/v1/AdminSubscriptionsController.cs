using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Api.Filters;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.AdminSubscriptions.Commands.AssignSubscription;
using SaaS.Application.Features.AdminSubscriptions.Commands.RemoveSubscription;
using SaaS.Application.Features.AdminSubscriptions.Queries.GetSubscription;
using SaaS.Application.Features.AdminSubscriptions.Queries.GetSubscriptions;
using SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin;
using System.Security.Claims;

namespace SaaS.Api.Controllers.v1
{
    [Route("api/v1/admin/subscriptions")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [AdminSubscriptionsRequestValidation]
    public class AdminSubscriptionsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ILogger<AdminSubscriptionsController> _logger;

        public AdminSubscriptionsController(ISender sender, ILogger<AdminSubscriptionsController> logger)
        {
            _sender = sender;
            _logger = logger;
        }

        private Guid GetAdminId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ? adminId : Guid.Empty;

        [HttpGet]
        public async Task<ActionResult<object>> List(CancellationToken cancellationToken, [FromQuery] Guid? userId = null, [FromQuery] int? botId = null, [FromQuery] bool? isActive = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var query = new GetAdminSubscriptionsQuery(userId, botId, isActive, pageNumber, pageSize);
            var result = await _sender.Send(query, cancellationToken);

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

        [HttpGet("{id:int}")]
        public async Task<ActionResult<object>> Get(int id, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var query = new GetAdminSubscriptionQuery(id);
            var result = await _sender.Send(query, cancellationToken);

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

        [HttpPost]
        public async Task<ActionResult<object>> Assign([FromBody] AssignAdminSubscriptionDto request, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var command = new AssignAdminSubscriptionCommand(request);
            var result = await _sender.Send(command, cancellationToken);

            if (result is not null && result.IsSuccess)
            {
                if (result.Data is AdminSubscriptionDto subscription)
                {
                    return CreatedAtAction(nameof(Get), new { id = subscription.Id }, result);
                }

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

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<object>> Remove(int id, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var command = new RemoveAdminSubscriptionCommand(id);
            var result = await _sender.Send(command, cancellationToken);

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

        private async Task<ObjectResult?> ValidateAdminAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Validating admin identity and role for request path: {Path}", HttpContext.Request.Path);
            var adminId = GetAdminId();
            if (adminId == Guid.Empty)
            {
                return Error(StatusCodes.Status401Unauthorized, "Invalid admin identity.");
            }

            // Reuse the existing SystemAdmin lookup to reject inactive accounts and stale roles.
            var query = new GetCurrentAdminQuery(adminId);
            var result = await _sender.Send(query, cancellationToken);

            if (!result.IsSuccess || result.Data is null)
            {
                return Error(StatusCodes.Status401Unauthorized, result.Message);
            }

            if (result.Data.Role is not ("Admin" or "SuperAdmin") || !User.IsInRole(result.Data.Role))
            {
                return Error(StatusCodes.Status403Forbidden, "Administrative access is required.");
            }

            return null;
        }

        private ObjectResult Error(int statusCode, string message)
        {
            var errorResponse = new ApiErrorResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = message,
                Path = HttpContext.Request.Path,
                Method = HttpContext.Request.Method,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }
    }
}
