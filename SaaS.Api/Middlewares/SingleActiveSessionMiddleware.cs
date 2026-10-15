using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SaaS.Application.Common.Interfaces;

namespace SaaS.Api.Middlewares;

public class SingleActiveSessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SingleActiveSessionMiddleware> _logger;

    public SingleActiveSessionMiddleware(RequestDelegate next, ILogger<SingleActiveSessionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISessionTokenValidator sessionValidator)
    {
        _logger.LogInformation("Checking for single active session...");

        string[] roles = ["Admin", "SuperAdmin", "SystemAdmin"];

        if (context.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User is authenticated. Checking roles and session token...");
            var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;

            if (roles.Contains(userRole))
            {
                _logger.LogInformation("User has an admin role. Skipping session token validation.");

                await _next(context);
                return;
            }

            _logger.LogInformation("User does not have an admin role. Validating session token...");
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tokenSessionClaim = context.User.FindFirst("SessionToken")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId) || string.IsNullOrEmpty(tokenSessionClaim))
            {
                _logger.LogWarning("Invalid user ID or session token claim.");

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Invalid session token." });
                return;
            }

            _logger.LogInformation("Retrieving current session token from the database for user ID: {UserId}", userId);

            var currentDbSessionToken = await sessionValidator.GetCurrentSessionTokenAsync(userId);

            if (currentDbSessionToken == null || currentDbSessionToken != tokenSessionClaim)
            {
                _logger.LogWarning("Session token mismatch or expired for user ID: {UserId}.", userId);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Your session has expired or ended because you logged in from another device."
                });
                return;
            }

            _logger.LogInformation("Session token validated successfully for user ID: {UserId}. Proceeding to the next middleware.", userId);
        }

        await _next(context);
    }
}
