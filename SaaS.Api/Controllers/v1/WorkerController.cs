using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SaaS.Api.Extensions;
using SaaS.Api.Filters;
using SaaS.Application.Common.Models;
using SaaS.Application.Features.Worker.Commands.UpdateLeadStatus;
using SaaS.Application.Features.Worker.Commands.UpdateJobStatus;
using SaaS.Application.Features.Worker.Commands.LogBotActivity;
using SaaS.Domain.Enums;
using System.Security.Claims;
using SaaS.Application.Common.Dtos;
using SaaS.Application.Features.Worker.Commands.DispatchMessaging;
using SaaS.Application.Features.Worker.Commands.UpdateAccountStatus;
using Microsoft.AspNetCore.Authorization;

namespace SaaS.Api.Controllers.v1
{
    [ApiController]
    [Route("api/v1/worker")]
    public class WorkerController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<WorkerController> _logger;

        public WorkerController(IMediator mediator, ILogger<WorkerController> logger)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;

        [HttpPost("dispatch-messaging")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<DispatchMessagingResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DispatchMessaging([FromBody] DispatchMessagingDto messagingDto)
        {
            _logger.LogInformation("Worker initiated POST request to dispatch messaging for Bot: {BotId}", messagingDto.BotId);

            var userId = GetUserId();
            var command = new DispatchMessagingJobCommand(userId, messagingDto.BotId, messagingDto.AccountId, messagingDto.LeadIds);

            var result = await _mediator.Send(command);

            if (result is not null && result.IsSuccess)
            {
                _logger.LogInformation("Successfully dispatch messaging for Bot: {BotId}", messagingDto.BotId);
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

            _logger.LogWarning("Failed to dispatch messaging for Bot: {BotId}. Reason: {Message}", messagingDto.BotId, errorResponse.Message);
            return StatusCode(statusCode, errorResponse);
        }

        [HttpPut("leads/{leadId}/status")]
        [BotWorkerAuthorize]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateLeadStatus(long leadId, [FromBody] UpdateStatusDto statusDto)
        {
            _logger.LogInformation("Worker initiated PUT request to update lead {LeadId} to status {Status}", leadId, statusDto.Status);

            var command = new UpdateLeadStatusCommand(leadId, statusDto.Status);

            var result = await _mediator.Send(command);

            if (result is not null && result.IsSuccess)
            {
                _logger.LogInformation("Successfully updated lead {LeadId} to status {Status}", leadId, statusDto.Status);
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

            _logger.LogWarning("Failed to update lead {LeadId}. Reason: {Message}", leadId, errorResponse.Message);
            return StatusCode(statusCode, errorResponse);
        }

        [HttpPut("jobs/{jobId}/status")]
        [BotWorkerAuthorize]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateJobStatus(long jobId, [FromBody] UpdateStatusDto statusDto)
        {
            _logger.LogInformation("Worker initiated PUT request to update job {JobId} to status {Status}", jobId, statusDto.Status);

            // Bind the route parameter to the command model
            var command = new UpdateJobStatusCommand(jobId, statusDto.Status);

            var result = await _mediator.Send(command);

            if (result is not null && result.IsSuccess)
            {
                _logger.LogInformation("Successfully updated job {JobId} to status {Status}", jobId, statusDto.Status);
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

            _logger.LogWarning("Failed to update job {JobId}. Reason: {Message}", jobId, errorResponse.Message);
            return StatusCode(statusCode, errorResponse);
        }

        [HttpPut("accounts/{accountId}/status")]
        [BotWorkerAuthorize]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAccountStatus(int accountId, [FromBody] UpdateStatusDto statusDto)
        {
            _logger.LogInformation("Worker initiated POST request to update account {AccountId} to status {Status}", accountId, statusDto.Status);

            var command = new UpdateAccountStatusCommand(accountId, statusDto.Status);

            var result = await _mediator.Send(command);

            if (result is not null && result.IsSuccess)
            {
                _logger.LogInformation("Successfully updated account {AccountId} to status {Status}", accountId, statusDto.Status);
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

            _logger.LogWarning("Failed to update account {AccountId}. Reason: {Message}", accountId, errorResponse.Message);
            return StatusCode(statusCode, errorResponse);
        }

        [HttpPost("activity-log")]
        [BotWorkerAuthorize]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> LogBotActivity([FromBody] LogBotActivityCommand command)
        {
            _logger.LogInformation("Worker initiated POST request to log bot activity. CorrelationId: {CorrelationId}", command.CorrelationId);

            var result = await _mediator.Send(command);

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

            _logger.LogWarning("Failed to log bot activity. Reason: {Message}", errorResponse.Message);
            return StatusCode(statusCode, errorResponse);
        }
    }
}
