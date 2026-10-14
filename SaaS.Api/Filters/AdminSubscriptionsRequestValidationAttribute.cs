using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SaaS.Application.Common.Models;

namespace SaaS.Api.Filters;

public sealed class AdminSubscriptionsRequestValidationAttribute : ActionFilterAttribute
{
    public AdminSubscriptionsRequestValidationAttribute() => Order = -3000;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
            return;
        context.Result = new BadRequestObjectResult(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Message = "Invalid subscription management request. Supply valid values and only supported fields.",
            Path = context.HttpContext.Request.Path, Method = context.HttpContext.Request.Method,
            TraceId = context.HttpContext.TraceIdentifier
        });
    }
}
