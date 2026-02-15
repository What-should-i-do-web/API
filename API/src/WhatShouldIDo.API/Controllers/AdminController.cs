using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IPlaceService _placeService;
        private readonly ISubscriptionService _subscriptionService;

        public AdminController(
            IPlaceService placeService,
            ISubscriptionService subscriptionService)
        {
            _placeService = placeService;
            _subscriptionService = subscriptionService;
        }

        [HttpPut("place/sponsor")]
        public async Task<IActionResult> UpdateSponsorship([FromBody] UpdatePlaceSponsorshipRequest request)
        {
            var result = await _placeService.UpdateSponsorshipAsync(request);
            if (!result)
                return NotFound(new { message = "Place not found." });

            return Ok(new { message = "Sponsorship updated successfully." });
        }

        /// <summary>
        /// Manually grants a subscription to a user (admin-only).
        /// Used for beta testers, compensation, or internal testing.
        /// </summary>
        /// <param name="request">The grant request with userId, plan, expiration, and notes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated subscription</returns>
        [HttpPost("subscriptions/grant")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ManualGrant(
            [FromBody] ManualGrantRequest request,
            CancellationToken cancellationToken = default)
        {
            var adminUserId = GetCurrentUserId();
            if (adminUserId == Guid.Empty)
            {
                return Unauthorized(new { message = "Unable to determine admin user identity." });
            }

            try
            {
                var subscription = await _subscriptionService.ManualGrantAsync(request, adminUserId, cancellationToken);
                return Ok(new
                {
                    message = "Manual grant applied successfully.",
                    subscription
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Revokes a manual grant from a user, resetting them to free tier (admin-only).
        /// Only works for subscriptions with Provider=Manual.
        /// </summary>
        /// <param name="userId">The user ID whose grant to revoke</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Success or failure message</returns>
        [HttpDelete("subscriptions/grant/{userId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RevokeManualGrant(
            [FromRoute] Guid userId,
            CancellationToken cancellationToken = default)
        {
            var adminUserId = GetCurrentUserId();
            if (adminUserId == Guid.Empty)
            {
                return Unauthorized(new { message = "Unable to determine admin user identity." });
            }

            var revoked = await _subscriptionService.RevokeManualGrantAsync(userId, adminUserId, cancellationToken);

            if (!revoked)
            {
                return NotFound(new { message = "User has no manual grant to revoke." });
            }

            return Ok(new { message = "Manual grant revoked successfully. User reset to free tier." });
        }

        /// <summary>
        /// Gets a user's subscription details (admin-only, for support purposes).
        /// </summary>
        /// <param name="userId">The user ID to look up</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The user's subscription details</returns>
        [HttpGet("subscriptions/{userId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUserSubscription(
            [FromRoute] Guid userId,
            CancellationToken cancellationToken = default)
        {
            var subscription = await _subscriptionService.GetMySubscriptionAsync(userId, cancellationToken);
            return Ok(subscription);
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
    }
}
