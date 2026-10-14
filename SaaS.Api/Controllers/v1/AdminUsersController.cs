using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Api.Filters;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.AdminUsers.Commands.CreateUser;
using SaaS.Application.Features.AdminUsers.Commands.UpdateUser;
using SaaS.Application.Features.AdminUsers.Commands.SetUserStatus;
using SaaS.Application.Features.AdminUsers.Queries.GetUser;
using SaaS.Application.Features.AdminUsers.Queries.GetUsers;
using SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin;
using System.Security.Claims;

namespace SaaS.Api.Controllers.v1
{
    [Route("api/v1/admin/users")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [AdminUsersRequestValidation]
    public class AdminUsersController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ILogger<AdminUsersController> _logger;

        public AdminUsersController(ISender sender, ILogger<AdminUsersController> logger)
        {
            _sender = sender;
            _logger = logger;
        }

        private Guid GetAdminId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ? adminId : Guid.Empty;

        [HttpGet]
        public async Task<ActionResult<object>> List(CancellationToken cancellationToken, [FromQuery] string? search = null, [FromQuery] bool? isDeleted = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var query = new GetAdminUsersQuery(search, isDeleted, pageNumber, pageSize);
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

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<object>> Get(Guid id, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var query = new GetAdminUserQuery(id);
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
        public async Task<ActionResult<object>> Create([FromBody] CreateAdminUserDto request, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var command = new CreateAdminUserCommand(request);
            var result = await _sender.Send(command, cancellationToken);

            if (result is not null && result.IsSuccess)
            {
                if (result.Data is AdminUserDto user)
                {
                    return CreatedAtAction(nameof(Get), new { id = user.Id }, result);
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

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<object>> Update(Guid id, [FromBody] UpdateAdminUserDto request, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                return authorizationError;
            }

            var command = new UpdateAdminUserCommand(id, request);
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

        [HttpPut("{id:guid}/deactivate")]
        public async Task<ActionResult<object>> Deactivate(Guid id, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                return authorizationError;
            }

            var command = new SetAdminUserStatusCommand(id, true);
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

        [HttpPut("{id:guid}/reactivate")]
        public async Task<ActionResult<object>> Reactivate(Guid id, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                return authorizationError;
            }

            var command = new SetAdminUserStatusCommand(id, false);
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
