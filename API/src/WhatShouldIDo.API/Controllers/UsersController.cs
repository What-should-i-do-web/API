using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    /// <summary>
    /// Controller for user profile and personalization features
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserHistoryRepository _userHistoryRepository;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserHistoryRepository userHistoryRepository,
            ILogger<UsersController> logger)
        {
            _userHistoryRepository = userHistoryRepository ?? throw new ArgumentNullException(nameof(userHistoryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get user's route history (MRU - last 3 routes by default)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="take">Number of routes to return (default: 3, max: 10)</param>
        /// <returns>User's route history</returns>
        [HttpGet("{userId:guid}/history/routes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetRouteHistory(Guid userId, [FromQuery] int take = 3)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue || currentUserId.Value != userId)
                {
                    return Forbid();
                }

                // Limit to max 10
                take = Math.Min(take, 10);

                var history = await _userHistoryRepository.GetUserRouteHistoryAsync(
                    userId,
                    take,
                    HttpContext.RequestAborted);

                _logger.LogInformation("Retrieved {Count} route history items for user {UserId}", history.Count(), userId);

                return Ok(new
                {
                    success = true,
                    data = history.Select(h => new
                    {
                        h.Id,
                        h.RouteName,
                        h.RouteId,
                        h.PlaceCount,
                        h.Source,
                        h.CreatedAt,
                        h.SequenceNumber
                    }),
                    metadata = new
                    {
                        userId,
                        count = history.Count(),
                        mruLimit = 3
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving route history for user {UserId}", userId);
                return StatusCode(500, new { error = "An error occurred while retrieving route history" });
            }
        }

        /// <summary>
        /// Get user's suggestion history (MRU - last 20 places by default)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="take">Number of suggestions to return (default: 20, max: 50)</param>
        /// <returns>User's recently suggested places</returns>
        [HttpGet("{userId:guid}/history/places")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetSuggestionHistory(Guid userId, [FromQuery] int take = 20)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue || currentUserId.Value != userId)
                {
                    return Forbid();
                }

                // Limit to max 50
                take = Math.Min(take, 50);

                var history = await _userHistoryRepository.GetRecentSuggestionsAsync(
                    userId,
                    take,
                    HttpContext.RequestAborted);

                _logger.LogInformation("Retrieved {Count} suggestion history items for user {UserId}", history.Count(), userId);

                return Ok(new
                {
                    success = true,
                    data = history.Select(h => new
                    {
                        h.Id,
                        h.PlaceId,
                        h.PlaceName,
                        h.Category,
                        h.Source,
                        h.SuggestedAt,
                        h.SessionId,
                        h.SequenceNumber
                    }),
                    metadata = new
                    {
                        userId,
                        count = history.Count(),
                        mruLimit = 20
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving suggestion history for user {UserId}", userId);
                return StatusCode(500, new { error = "An error occurred while retrieving suggestion history" });
            }
        }

        /// <summary>
        /// Get user's favorites
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User's favorite places</returns>
        [HttpGet("{userId:guid}/favorites")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetFavorites(Guid userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue || currentUserId.Value != userId)
                {
                    return Forbid();
                }

                var favorites = await _userHistoryRepository.GetUserFavoritesAsync(
                    userId,
                    HttpContext.RequestAborted);

                return Ok(new
                {
                    success = true,
                    data = favorites.Select(f => new
                    {
                        f.Id,
                        f.PlaceId,
                        f.PlaceName,
                        f.Category,
                        f.Latitude,
                        f.Longitude,
                        f.Notes,
                        f.AddedAt
                    }),
                    count = favorites.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving favorites for user {UserId}", userId);
                return StatusCode(500, new { error = "An error occurred while retrieving favorites" });
            }
        }

        /// <summary>
        /// Get user's exclusions
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>User's excluded places (active only)</returns>
        [HttpGet("{userId:guid}/exclusions")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetExclusions(Guid userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (!currentUserId.HasValue || currentUserId.Value != userId)
                {
                    return Forbid();
                }

                var exclusions = await _userHistoryRepository.GetActiveExclusionsAsync(
                    userId,
                    HttpContext.RequestAborted);

                return Ok(new
                {
                    success = true,
                    data = exclusions.Select(e => new
                    {
                        e.Id,
                        e.PlaceId,
                        e.PlaceName,
                        e.Reason,
                        e.ExcludedAt,
                        e.ExpiresAt,
                        e.IsPermanent
                    }),
                    count = exclusions.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving exclusions for user {UserId}", userId);
                return StatusCode(500, new { error = "An error occurred while retrieving exclusions" });
            }
        }

        /// <summary>
        /// Get current user ID from claims
        /// </summary>
        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             User.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
