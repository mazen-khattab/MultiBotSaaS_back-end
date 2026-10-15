using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Auth.Commands.User;
using SaaS.Application.Features.Auth.Commands.User.Login;
using SaaS.Application.Features.Auth.Commands.User.Logout;
using SaaS.Application.Features.Auth.Commands.User.RefreshToken;
using SaaS.Application.Features.Auth.Queries.User.GetCurrentUser;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaS.Api.Controllers.v1
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ISender sender, ITokenService tokenService, ILogger<AuthController> logger)
        {
            _sender = sender;
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>
        /// User login endpoint.
        /// Accepts email and password, sends UserLoginCommand via MediatR.
        /// Returns user DTO with status 200 OK.
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<AuthLoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            var command = new UserLoginCommand(request.Email, request.Password);
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
                Message = result?.Message ?? "Failure to Login.",
                Path = "api/v1/auth/login",
                Method = HttpMethods.Post,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Refresh token endpoint.
        /// Reads refresh token from HTTP-Only cookie.
        /// Returns 401 Unauthorized if token is null.
        /// Sends UserRefreshTokenCommand via MediatR.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthLoginResponseDto>> Refresh()
        {
            _logger.LogInformation("Attempting to refresh token for user.");
            var refreshToken = _tokenService.GetCookiesRefreshToken();

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized("Refresh token does not exist in cookies");
            }

            var command = new UserRefreshTokenCommand(refreshToken);
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
                Message = result?.Message ?? "Failure to Refresh tokens.",
                Path = "api/v1/auth/refresh",
                Method = HttpMethods.Post,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// User logout endpoint.
        /// Requires [Authorize] attribute.
        /// Extracts UserId from claims (ClaimTypes.NameIdentifier).
        /// Reads refresh token from HTTP-Only cookie.
        /// Sends LogoutUserCommand via MediatR.
        /// Cookie cleanup is handled inside command/token generator.
        /// </summary>
        /// <summary>
        /// Get the currently authenticated user's profile.
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<AuthLoginResponseDto>> GetMe()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var role = User.FindFirstValue(ClaimTypes.Role) ?? "User";
            var query = new GetCurrentUserQuery(userId);
            var result = await _sender.Send(query);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result?.ErrorType.ToHttpStatusCode() ?? StatusCodes.Status401Unauthorized;

            var errorResponse = new ApiErrorResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = result?.Message ?? "Unauthorized",
                Path = "api/v1/auth/me",
                Method = HttpMethods.Get,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// User logout endpoint.
        /// Requires [Authorize] attribute.
        /// Extracts UserId from claims (ClaimTypes.NameIdentifier).
        /// Reads refresh token from HTTP-Only cookie.
        /// Sends LogoutUserCommand via MediatR.
        /// Cookie cleanup is handled inside command/token generator.
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdString, out var userId))
            {
                return BadRequest("Invalid user ID in claims.");
            }

            var refreshToken = _tokenService.GetCookiesRefreshToken();

            var command = new LogoutUserCommand(userId, refreshToken);
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
                Message = result?.Message ?? "Failure to Logout.",
                Path = "api/v1/auth/logout",
                Method = HttpMethods.Post,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }
    }
}
