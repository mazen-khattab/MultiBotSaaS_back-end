using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SaaS.Application.Common.Models;

namespace SaaS.Api.Filters;

// Keep JSON binding failures in the same error envelope as CQRS validation failures.
public sealed class SettingsRequestValidationAttribute : ActionFilterAttribute
{
    public SettingsRequestValidationAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
            return;

        context.Result = new BadRequestObjectResult(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = "Invalid settings request. Supply dailyLeadLimit as an integer and only supported settings fields.",
            Path = context.HttpContext.Request.Path,
            Method = context.HttpContext.Request.Method,
            TraceId = context.HttpContext.TraceIdentifier
        });
    }
}
