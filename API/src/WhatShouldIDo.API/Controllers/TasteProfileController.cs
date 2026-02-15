using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhatShouldIDo.API.Models;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    /// <summary>
    /// Taste profile management endpoints.
    /// Requires authentication.
    /// </summary>
    [ApiController]
    [Route("api/taste-profile")]
    [Authorize]
    public class TasteProfileController : ControllerBase
    {
        private readonly ITasteProfileService _profileService;
        private readonly ILogger<TasteProfileController> _logger;

        public TasteProfileController(
            ITasteProfileService profileService,
            ILogger<TasteProfileController> logger)
        {
            _profileService = profileService;
            _logger = logger;
        }

        /// <summary>
        /// Get the authenticated user's taste profile.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>User's taste profile or 404 if not found</returns>
        [HttpGet("me")]
        [ProducesResponseType(typeof(TasteProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Status = 401,
                    Title = "Unauthorized",
                    Detail = "User ID not found in token"
                });
            }

            var profile = await _profileService.GetProfileAsync(userId.Value, cancellationToken);

            if (profile == null)
            {
                return NotFound(new ErrorResponse
                {
                    Status = 404,
                    Title = "Profile Not Found",
                    Detail = "You haven't completed the taste quiz yet. Please complete onboarding first."
                });
            }

            return Ok(profile);
        }

        /// <summary>
        /// Manually update taste profile weights.
        /// Useful for fine-tuning preferences.
        /// </summary>
        /// <param name="request">Weight updates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated profile</returns>
        [HttpPatch("me")]
        [ProducesResponseType(typeof(TasteProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateTasteProfileRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Status = 401,
                    Title = "Unauthorized",
                    Detail = "User ID not found in token"
                });
            }

            try
            {
                var profile = await _profileService.UpdateProfileAsync(
                    userId.Value,
                    request.Weights,
                    cancellationToken
                );

                _logger.LogInformation(
                    "User {UserId} manually updated taste profile ({WeightCount} weights changed)",
                    userId.Value,
                    request.Weights.Count
                );

                return Ok(profile);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new ErrorResponse
                {
                    Status = 404,
                    Title = "Profile Not Found",
                    Detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Apply feedback to evolve taste profile.
        /// Bounded deltas prevent drastic changes.
        /// </summary>
        /// <param name="request">Place feedback (like, dislike, skip)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Feedback response with updated profile summary</returns>
        [HttpPost("feedback")]
        [ProducesResponseType(typeof(PlaceFeedbackResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApplyFeedback(
            [FromBody] PlaceFeedbackRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Status = 401,
                    Title = "Unauthorized",
                    Detail = "User ID not found in token"
                });
            }

            try
            {
                var response = await _profileService.ApplyFeedbackAsync(
                    userId.Value,
                    request,
                    cancellationToken
                );

                _logger.LogInformation(
                    "User {UserId} gave {Feedback} feedback on category {Category}",
                    userId.Value,
                    request.FeedbackType,
                    request.PlaceCategory
                );

                return Ok(response);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new ErrorResponse
                {
                    Status = 404,
                    Title = "Profile Not Found",
                    Detail = "Please complete the taste quiz first."
                });
            }
        }

        // TODO: Implement GetEventHistoryAsync in ITasteProfileService
        // /// <summary>
        // /// Get taste profile event history.
        // /// Shows how profile has evolved over time.
        // /// </summary>
        // /// <param name="limit">Max events to return (default 50)</param>
        // /// <param name="cancellationToken">Cancellation token</param>
        // /// <returns>List of profile events</returns>
        // [HttpGet("history")]
        // [ProducesResponseType(typeof(List<TasteEventDto>), StatusCodes.Status200OK)]
        // public async Task<IActionResult> GetHistory(
        //     [FromQuery] int limit = 50,
        //     CancellationToken cancellationToken = default)
        // {
        //     var userId = GetUserId();
        //     if (userId == null)
        //     {
        //         return Unauthorized(new ErrorResponse
        //         {
        //             Status = 401,
        //             Title = "Unauthorized",
        //             Detail = "User ID not found in token"
        //         });
        //     }
        //
        //     var events = await _profileService.GetEventHistoryAsync(
        //         userId.Value,
        //         Math.Min(limit, 100), // Cap at 100
        //         cancellationToken
        //     );
        //
        //     var response = events.Select(e => new TasteEventDto
        //     {
        //         EventId = e.Id,
        //         UserId = e.UserId,
        //         EventType = e.EventType,
        //         OccurredAt = e.OccurredAtUtc,
        //         Payload = e.Payload
        //     }).ToList();
        //
        //     return Ok(response);
        // }

        private Guid? GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
        }
    }
}
