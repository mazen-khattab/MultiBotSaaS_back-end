using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Api.Filters;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.AdminBots.Commands.CreateBot;
using SaaS.Application.Features.AdminBots.Commands.UpdateBot;
using SaaS.Application.Features.AdminBots.Commands.SetBotStatus;
using SaaS.Application.Features.AdminBots.Queries.GetBot;
using SaaS.Application.Features.AdminBots.Queries.GetBots;
using SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin;
using System.Security.Claims;

namespace SaaS.Api.Controllers.v1
{
    [Route("api/v1/admin/bots")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    [AdminBotsRequestValidation]
    public class AdminBotsController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ILogger<AdminBotsController> _logger;

        public AdminBotsController(ISender sender, ILogger<AdminBotsController> logger)
        {
            _sender = sender;
            _logger = logger;
        }

        private Guid GetAdminId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ? adminId : Guid.Empty;

        [HttpGet]
        public async Task<ActionResult<object>> List(CancellationToken cancellationToken, [FromQuery] string? search = null, [FromQuery] bool? isActive = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var query = new GetAdminBotsQuery(search, isActive, pageNumber, pageSize);
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

            var query = new GetAdminBotQuery(id);
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
        public async Task<ActionResult<object>> Create([FromBody] CreateAdminBotDto request, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                _logger.LogWarning("Admin authorization failed for request path: {Path}", HttpContext.Request.Path);
                return authorizationError;
            }

            var command = new CreateAdminBotCommand(request);
            var result = await _sender.Send(command, cancellationToken);

            if (result is not null && result.IsSuccess)
            {
                if (result.Data is AdminBotDto bot)
                {
                    return CreatedAtAction(nameof(Get), new { id = bot.Id }, result);
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

        [HttpPut("{id:int}")]
        public async Task<ActionResult<object>> Update(int id, [FromBody] UpdateAdminBotDto request, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                return authorizationError;
            }

            var command = new UpdateAdminBotCommand(id, request);
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

        [HttpPut("{id:int}/deactivate")]
        public async Task<ActionResult<object>> Deactivate(int id, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                return authorizationError;
            }

            var command = new SetAdminBotStatusCommand(id, false);
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

        [HttpPut("{id:int}/activate")]
        public async Task<ActionResult<object>> Activate(int id, CancellationToken cancellationToken)
        {
            var authorizationError = await ValidateAdminAsync(cancellationToken);
            if (authorizationError is not null)
            {
                return authorizationError;
            }

            var command = new SetAdminBotStatusCommand(id, true);
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
