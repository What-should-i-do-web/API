using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using WhatShouldIDo.API.Attributes;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.UseCases.Commands;
using WhatShouldIDo.Application.UseCases.Queries;

namespace WhatShouldIDo.API.Controllers
{
    /// <summary>
    /// Controller for managing user subscriptions.
    /// Provides endpoints for getting subscription status and verifying receipts.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [SkipQuota] // Subscription endpoints don't consume quota
    public class SubscriptionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly SubscriptionOptions _options;
        private readonly ILogger<SubscriptionsController> _logger;

        public SubscriptionsController(
            IMediator mediator,
            IOptions<SubscriptionOptions> options,
            ILogger<SubscriptionsController> logger)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current user's subscription status
        /// </summary>
        /// <returns>The user's subscription details</returns>
        /// <response code="200">Returns the subscription details</response>
        /// <response code="401">User is not authenticated</response>
        [HttpGet("me")]
        [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMySubscription(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            try
            {
                var query = new GetMySubscriptionQuery(userId.Value);
                var subscription = await _mediator.Send(query, cancellationToken);
                return Ok(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subscription for user {UserId}", userId);
                return StatusCode(500, new { error = "Failed to retrieve subscription information" });
            }
        }

        /// <summary>
        /// Verifies a subscription receipt from Apple App Store or Google Play.
        /// Note: This endpoint is disabled by default. Enable via configuration for mobile app integration.
        /// </summary>
        /// <param name="request">The verification request containing receipt data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The verification result including updated subscription status</returns>
        /// <response code="200">Receipt verified successfully</response>
        /// <response code="400">Invalid request</response>
        /// <response code="401">User is not authenticated</response>
        /// <response code="403">Verification failed or access denied</response>
        /// <response code="501">Verification is not enabled on this environment</response>
        [HttpPost("verify")]
        [ProducesResponseType(typeof(VerifySubscriptionResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status501NotImplemented)]
        public async Task<IActionResult> VerifyReceipt(
            [FromBody] VerifyReceiptRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Log without exposing the receipt content (security)
                _logger.LogInformation(
                    "Receipt verification request from user {UserId}, Provider: {Provider}, Plan: {Plan}",
                    userId, request.Provider, request.Plan);

                var command = new VerifySubscriptionReceiptCommand(userId.Value, request);
                var result = await _mediator.Send(command, cancellationToken);

                // Handle disabled state
                if (!result.Success && result.ErrorCode == "VERIFICATION_DISABLED")
                {
                    return StatusCode(StatusCodes.Status501NotImplemented, new
                    {
                        type = "https://errors.whatshouldido.app/verification-disabled",
                        title = "Verification Not Implemented",
                        status = 501,
                        errorCode = result.ErrorCode,
                        detail = result.ErrorMessage
                    });
                }

                // Handle verification failure
                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        type = "https://errors.whatshouldido.app/verification-failed",
                        title = "Verification Failed",
                        status = 403,
                        errorCode = result.ErrorCode,
                        detail = result.ErrorMessage
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying receipt for user {UserId}", userId);
                return StatusCode(500, new { error = "Failed to verify receipt" });
            }
        }

        /// <summary>
        /// Gets information about the verification service status
        /// </summary>
        /// <returns>Whether verification is enabled and other configuration details</returns>
        [HttpGet("status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetVerificationStatus()
        {
            return Ok(new
            {
                verificationEnabled = _options.VerificationEnabled,
                devTestReceiptsAllowed = _options.AllowDevTestReceipts,
                supportedProviders = new[] { "AppleAppStore", "GooglePlay" },
                supportedPlans = new[] { "Monthly", "Yearly" }
            });
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             User.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
