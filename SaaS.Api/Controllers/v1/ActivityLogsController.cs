using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.ActivityLogs.Queries.GetActivityLogs;

namespace SaaS.Api.Controllers.v1;

[ApiController]
[Route("api/v1/activity-logs")]
[Authorize(Roles = "User")]
public sealed class ActivityLogsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<BotActivityLogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> Get(
        CancellationToken cancellationToken,
        [FromQuery] int? botId = null,
        [FromQuery] string? runId = null,
        [FromQuery] string? logLevel = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == Guid.Empty)
        {
            return Error(StatusCodes.Status401Unauthorized, "Invalid user identity.");
        }

        // MVC binds blank strings to null; keep supplied blanks so validation can reject them.
        if (Request.Query.ContainsKey("runId"))
        {
            runId ??= string.Empty;
        }
        if (Request.Query.ContainsKey("logLevel"))
        {
            logLevel ??= string.Empty;
        }

        var result = await sender.Send(new GetActivityLogsQuery(userId, botId, runId, logLevel, pageNumber, pageSize), cancellationToken);
        return result.IsSuccess ? Ok(result) : Error(result.ErrorType.ToHttpStatusCode(), result.Message);
    }

    private ObjectResult Error(int statusCode, string message) => StatusCode(statusCode, new ApiErrorResponse
    {
        StatusCode = statusCode,
        Message = message,
        Path = Request.Path,
        Method = Request.Method,
        TraceId = HttpContext.TraceIdentifier
    });
}
