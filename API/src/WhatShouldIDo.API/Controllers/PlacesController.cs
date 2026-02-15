using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Queries;
using System.Security.Claims;

namespace WhatShouldIDo.API.Controllers
{
    /// <summary>
    /// Controller for AI-powered place search and discovery
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PlacesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IAIService _aiService;
        private readonly IUserHistoryRepository _userHistoryRepository;
        private readonly ILogger<PlacesController> _logger;

        public PlacesController(
            IMediator mediator,
            IAIService aiService,
            IUserHistoryRepository userHistoryRepository,
            ILogger<PlacesController> logger)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _userHistoryRepository = userHistoryRepository ?? throw new ArgumentNullException(nameof(userHistoryRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Search places using natural language or structured filters with AI-powered interpretation
        /// </summary>
        /// <param name="query">Search query object</param>
        /// <returns>List of places matching the query</returns>
        [HttpPost("search")]
        [ProducesResponseType(typeof(SearchPlacesResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Search([FromBody] SearchPlacesQuery query)
        {
            if (query == null)
            {
                return BadRequest(new { error = "Query cannot be null" });
            }

            if (string.IsNullOrWhiteSpace(query.Query))
            {
                return BadRequest(new { error = "Search query text is required" });
            }

            try
            {
                // Get user ID if authenticated
                var userId = GetCurrentUserId();
                if (userId.HasValue)
                {
                    query.UserId = userId.Value;
                }

                _logger.LogInformation("Processing search request: {Query} at ({Lat}, {Lng})",
                    query.Query, query.Latitude, query.Longitude);

                var result = await _mediator.Send(query);

                return Ok(new
                {
                    success = true,
                    data = result,
                    metadata = new
                    {
                        usedAI = result.UsedAI,
                        aiConfidence = result.AIConfidence,
                        interpretedQuery = result.InterpretedQuery,
                        extractedCategories = result.ExtractedCategories
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing search request: {Query}", query.Query);
                return StatusCode(500, new
                {
                    success = false,
                    error = "An error occurred while processing your search",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Get AI-generated summary for a specific place
        /// </summary>
        /// <param name="id">Place ID</param>
        /// <param name="style">Summary style (brief, detailed, highlights)</param>
        /// <returns>AI-generated place summary</returns>
        [HttpGet("{id}/summary")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPlaceSummary(string id, [FromQuery] string style = "brief")
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { error = "Place ID is required" });
            }

            try
            {
                // Get user ID if authenticated
                var userId = GetCurrentUserId();

                _logger.LogInformation("Getting AI summary for place: {PlaceId} with style: {Style}",
                    id, style);

                // Create query
                var query = new GetPlaceSummaryQuery
                {
                    PlaceId = id,
                    Style = style,
                    UserId = userId
                };

                var result = await _mediator.Send(query);

                return Ok(new
                {
                    success = true,
                    data = result,
                    metadata = new
                    {
                        usedAI = result.UsedAI,
                        aiProvider = result.AIProvider,
                        generatedAt = DateTime.UtcNow
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Place not found or AI generation failed: {PlaceId}", id);
                return NotFound(new
                {
                    success = false,
                    error = "Place not found or AI summary generation failed",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting place summary for: {PlaceId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    error = "An error occurred while getting place summary",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Health check for AI service
        /// </summary>
        /// <returns>AI service health status</returns>
        [HttpGet("ai/health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAIHealth()
        {
            try
            {
                var isHealthy = await _aiService.IsHealthyAsync();

                return Ok(new
                {
                    success = true,
                    healthy = isHealthy,
                    provider = _aiService.ProviderName,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking AI service health");
                return Ok(new
                {
                    success = false,
                    healthy = false,
                    provider = _aiService.ProviderName,
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Add a place to user's favorites
        /// </summary>
        /// <param name="placeId">Place ID to favorite</param>
        /// <param name="request">Favorite metadata (name, category, etc.)</param>
        /// <returns>Created favorite</returns>
        [HttpPost("{placeId}/favorite")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddToFavorites(
            string placeId,
            [FromBody] AddFavoriteRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                var favorite = await _userHistoryRepository.AddFavoriteAsync(
                    userId.Value,
                    placeId,
                    request.PlaceName,
                    request.Category,
                    request.Latitude,
                    request.Longitude,
                    request.Notes,
                    HttpContext.RequestAborted);

                _logger.LogInformation("User {UserId} favorited place {PlaceId}", userId, placeId);

                return Ok(new
                {
                    success = true,
                    message = "Place added to favorites",
                    favorite = new
                    {
                        favorite.Id,
                        favorite.PlaceId,
                        favorite.PlaceName,
                        favorite.Category,
                        favorite.AddedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding place {PlaceId} to favorites", placeId);
                return StatusCode(500, new { error = "An error occurred while adding to favorites" });
            }
        }

        /// <summary>
        /// Remove a place from user's favorites
        /// </summary>
        /// <param name="placeId">Place ID to unfavorite</param>
        /// <returns>Success status</returns>
        [HttpDelete("{placeId}/favorite")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RemoveFromFavorites(string placeId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                var removed = await _userHistoryRepository.RemoveFavoriteAsync(
                    userId.Value,
                    placeId,
                    HttpContext.RequestAborted);

                if (!removed)
                {
                    return NotFound(new { error = "Favorite not found" });
                }

                _logger.LogInformation("User {UserId} unfavorited place {PlaceId}", userId, placeId);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing place {PlaceId} from favorites", placeId);
                return StatusCode(500, new { error = "An error occurred while removing from favorites" });
            }
        }

        /// <summary>
        /// Add a place to user's exclusion list (do not recommend)
        /// </summary>
        /// <param name="placeId">Place ID to exclude</param>
        /// <param name="request">Exclusion metadata (reason, TTL, etc.)</param>
        /// <returns>Created exclusion</returns>
        [HttpPost("{placeId}/exclude")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ExcludePlace(
            string placeId,
            [FromBody] ExcludePlaceRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                // Calculate expiration date
                DateTime? expiresAt = null;
                if (request.DaysToExpire.HasValue && request.DaysToExpire.Value > 0)
                {
                    expiresAt = DateTime.UtcNow.AddDays(request.DaysToExpire.Value);
                }

                var exclusion = await _userHistoryRepository.AddExclusionAsync(
                    userId.Value,
                    placeId,
                    request.PlaceName,
                    expiresAt,
                    request.Reason,
                    HttpContext.RequestAborted);

                _logger.LogInformation("User {UserId} excluded place {PlaceId} (expires: {ExpiresAt})",
                    userId, placeId, expiresAt);

                return Ok(new
                {
                    success = true,
                    message = "Place excluded from recommendations",
                    exclusion = new
                    {
                        exclusion.Id,
                        exclusion.PlaceId,
                        exclusion.PlaceName,
                        exclusion.ExcludedAt,
                        exclusion.ExpiresAt,
                        exclusion.IsPermanent,
                        exclusion.Reason
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error excluding place {PlaceId}", placeId);
                return StatusCode(500, new { error = "An error occurred while excluding the place" });
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

    /// <summary>
    /// Request to add a place to favorites
    /// </summary>
    public class AddFavoriteRequest
    {
        public string PlaceName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to exclude a place
    /// </summary>
    public class ExcludePlaceRequest
    {
        public string PlaceName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public int? DaysToExpire { get; set; } // Null = permanent exclusion
    }
}
