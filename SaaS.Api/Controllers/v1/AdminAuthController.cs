using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SaaS.Api.Extensions;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Auth.Commands.Admin;
using SaaS.Application.Features.Auth.Commands.Admin.Login;
using SaaS.Application.Features.Auth.Commands.Admin.Logout;
using SaaS.Application.Features.Auth.Commands.Admin.RefreshToken;
using SaaS.Application.Features.Auth.Queries.Admin.GetCurrentAdmin;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaS.Api.Controllers.v1
{
    [ApiController]
    [Route("api/v1/admin/auth")]
    public class AdminAuthController : ControllerBase
    {
        private readonly ISender _sender;
        private readonly ITokenService _tokenService;

        public AdminAuthController(ISender sender, ITokenService tokenService)
        {
            _sender = sender;
            _tokenService = tokenService;
        }

        /// <summary>
        /// Admin login endpoint.
        /// Accepts email and password, sends AdminLoginCommand via MediatR.
        /// Returns admin DTO with status 200 OK.
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<AuthLoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            var command = new AdminLoginCommand(request.Email, request.Password);
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
                Path = "api/v1/admin/auth/login",
                Method = HttpMethods.Post,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Admin refresh token endpoint.
        /// Reads refresh token from HTTP-Only cookie.
        /// Returns 401 Unauthorized if token is null.
        /// Sends AdminRefreshTokenCommand via MediatR.
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthLoginResponseDto>> Refresh()
        {
            var refreshToken = _tokenService.GetCookiesRefreshToken();

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized();
            }

            var command = new AdminRefreshTokenCommand(refreshToken);
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
                Path = "api/v1/admin/auth/refresh",
                Method = HttpMethods.Post,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Get the currently authenticated admin's profile.
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<AuthLoginResponseDto>> GetMe()
        {
            var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(adminIdString, out var adminId))
            {
                return Unauthorized();
            }

            var query = new GetCurrentAdminQuery(adminId);
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
                Path = "api/v1/admin/auth/me",
                Method = HttpMethods.Get,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }

        /// <summary>
        /// Admin logout endpoint.
        /// Requires [Authorize] attribute.
        /// Extracts AdminId from claims (ClaimTypes.NameIdentifier).
        /// Reads refresh token from HTTP-Only cookie.
        /// Sends LogoutAdminCommand via MediatR.
        /// Cookie cleanup is handled inside command/token generator.
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(adminIdString, out var adminId))
            {
                return BadRequest("Invalid admin ID in claims.");
            }

            var refreshToken = _tokenService.GetCookiesRefreshToken();

            var command = new LogoutAdminCommand(adminId, refreshToken);
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
                Path = "api/v1/admin/auth/logout",
                Method = HttpMethods.Post,
                TraceId = HttpContext.TraceIdentifier
            };

            return StatusCode(statusCode, errorResponse);
        }
    }
}
