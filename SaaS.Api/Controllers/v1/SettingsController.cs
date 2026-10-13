using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Api.Filters;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Settings.Commands.Manage;
using SaaS.Application.Features.Settings.Queries;

namespace SaaS.Api.Controllers.v1;

[ApiController]
[Route("api/v1/settings")]
[Authorize(Roles = "User")]
[SettingsRequestValidation]
public class SettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Get(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Error(StatusCodes.Status401Unauthorized, "Invalid user identity.");

        var result = await sender.Send(new GetUserSettingsQuery(userId), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut]
    public async Task<ActionResult<object>> Update([FromBody] UpdateUserSettingsDto request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Error(StatusCodes.Status401Unauthorized, "Invalid user identity.");

        var result = await sender.Send(new ManageUserSettingsCommand(
            userId, request.AIApiKey, request.ScraperToken, request.DailyLeadLimit,
            request.CompanyName, request.CompanyPitch), cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId != Guid.Empty;

    private ActionResult<object> ToActionResult<T>(ApiResponse<T> result) => result.IsSuccess
        ? Ok(result)
        : Error(result.ErrorType.ToHttpStatusCode(), result.Message);

    private ObjectResult Error(int statusCode, string message) => StatusCode(statusCode, new ApiErrorResponse
    {
        StatusCode = statusCode,
        Message = message,
        Path = Request.Path,
        Method = Request.Method,
        TraceId = HttpContext.TraceIdentifier
    });
}
