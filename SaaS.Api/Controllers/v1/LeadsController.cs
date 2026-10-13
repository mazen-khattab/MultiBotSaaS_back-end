using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SaaS.Api.Extensions;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Leads.Queries.GetAllLeads;
using SaaS.Application.Features.Leads.Queries.GetAllowedPendingLeads;
using System.Security.Claims;

namespace SaaS.Api.Controllers.v1
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize]
    public class LeadsController : ControllerBase
    {
        private readonly ISender _sender;

        public LeadsController(ISender sender)
        {
            _sender = sender;
        }

        private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

        [HttpGet("leads/{botId}")]
        public async Task<ActionResult<object>> GetAll(int botId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? statusFilter = null)
        {
            var userId = GetUserId();

            var query = new GetAllLeadsQuery(userId, botId, pageNumber, pageSize, statusFilter);
            var result = await _sender.Send(query);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result!.ErrorType.ToHttpStatusCode();

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

        [HttpGet("bot/messaging-preview/{botId}")]
        public async Task<ActionResult<object>> GetMessagingPreview(int botId)
        {
            var userId = GetUserId();

            var query = new GetAllowedPendingLeadsQuery(userId, botId);
            var result = await _sender.Send(query);

            if (result is not null && result.IsSuccess)
            {
                return Ok(result);
            }

            int statusCode = result!.ErrorType.ToHttpStatusCode();

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

    }
}
