using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Commands;
using WhatShouldIDo.Application.UseCases.Queries;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoutesController : ControllerBase
    {
        private readonly IRouteService _routeService;
        private readonly IMediator _mediator;
        private readonly ISmartSuggestionService _smartSuggestionService;
        private readonly ILogger<RoutesController> _logger;

        public RoutesController(
            IRouteService routeService,
            IMediator mediator,
            ISmartSuggestionService smartSuggestionService,
            ILogger<RoutesController> logger)
        {
            _routeService = routeService;
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _smartSuggestionService = smartSuggestionService ?? throw new ArgumentNullException(nameof(smartSuggestionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RouteDto>>> GetAll()
        {
            var routes = await _routeService.GetAllAsync();
            return Ok(routes);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<RouteDto>> GetById(Guid id)
        {
            var route = (await _routeService.GetAllAsync()).FirstOrDefault(r => r.Id == id);
            if (route == null) return NotFound();
            return Ok(route);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<RouteDto>> Create([FromBody] CreateRouteRequest request)
        {
            try
            {
                // Get current user ID
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                // Map to MediatR command
                var command = new CreateRouteCommand
                {
                    Name = request.Name,
                    Description = request.Description,
                    UserId = userId.Value,
                    PlaceIds = request.PlaceIds ?? new List<string>(),
                    OptimizeOrder = request.OptimizeOrder,
                    TransportationMode = request.TransportationMode ?? "walking",
                    RouteType = "custom",
                    Tags = request.Tags ?? new List<string>()
                };

                _logger.LogInformation("Creating route: {RouteName} for user: {UserId}", command.Name, userId);

                var created = await _mediator.Send(command);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid route creation request");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating route");
                return StatusCode(500, new { error = "An error occurred while creating the route" });
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             User.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<RouteDto>> Update(Guid id, [FromBody] UpdateRouteRequest request)
        {
            var updated = await _routeService.UpdateAsync(id, request);
            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _routeService.DeleteAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Generate an AI-driven personalized route using user preferences and diversity algorithms
        /// </summary>
        /// <param name="request">Request parameters for AI route generation</param>
        /// <returns>AI-generated personalized day plan</returns>
        [HttpPost("ai/generate")]
        [Authorize]
        [ProducesResponseType(typeof(DayPlanDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DayPlanDto>> GenerateAIRoute([FromBody] CreateAIDrivenRouteRequest request)
        {
            try
            {
                // Get current user ID
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                // Validate request
                if (request.DiversityFactor < 0.0 || request.DiversityFactor > 1.0)
                {
                    return BadRequest(new { error = "Diversity factor must be between 0.0 and 1.0" });
                }

                _logger.LogInformation("Generating AI route for user {UserId} at ({Lat}, {Lng}) with diversity {Epsilon}",
                    userId, request.Latitude, request.Longitude, request.DiversityFactor);

                // Map to MediatR command
                var command = new CreateAIDrivenRouteCommand
                {
                    UserId = userId.Value,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    LocationName = request.LocationName,
                    RadiusKm = request.RadiusKm,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    PreferredCategories = request.PreferredCategories ?? new List<string>(),
                    AvoidedCategories = request.AvoidedCategories ?? new List<string>(),
                    Budget = request.Budget,
                    Transportation = request.Transportation,
                    IncludeMeals = request.IncludeMeals,
                    SpecialRequests = request.SpecialRequests,
                    DiversityFactor = request.DiversityFactor
                };

                var dayPlan = await _mediator.Send(command);

                return Ok(new
                {
                    success = true,
                    data = dayPlan,
                    metadata = new
                    {
                        aiGenerated = true,
                        diversityFactor = request.DiversityFactor,
                        activityCount = dayPlan.Activities.Count,
                        totalDistance = dayPlan.TotalDistance,
                        generatedAt = DateTime.UtcNow
                    }
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid AI route generation request");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot generate AI route");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI route");
                return StatusCode(500, new { error = "An error occurred while generating the AI route" });
            }
        }

        /// <summary>
        /// Generates a personalized "Surprise Me" route using AI and user preferences.
        /// Implements exclusion window logic and respects user's favorites/exclusions.
        /// </summary>
        /// <param name="request">Surprise Me request parameters</param>
        /// <returns>Optimized personalized route with AI-generated suggestions</returns>
        [HttpPost("surprise")]
        [Authorize]
        [ProducesResponseType(typeof(SurpriseMeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SurpriseMeResponse>> GenerateSurpriseRoute([FromBody] SurpriseMeRequest request)
        {
            try
            {
                // Get current user ID
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                _logger.LogInformation("Generating Surprise Me route for user {UserId} in {Area}",
                    userId, request.TargetArea);

                // Generate surprise route
                var response = await _smartSuggestionService.GenerateSurpriseRouteAsync(
                    userId.Value,
                    request,
                    HttpContext.RequestAborted);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid Surprise Me request");
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot generate Surprise Me route");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Surprise Me route");
                return StatusCode(500, new { error = "An error occurred while generating the surprise route" });
            }
        }

        // ===== Route Sharing Endpoints =====

        /// <summary>
        /// Create a share token for a route (allows read-only access via URL)
        /// </summary>
        /// <param name="id">Route ID</param>
        /// <param name="request">Share token options</param>
        /// <returns>Share token with URL</returns>
        [HttpPost("{id:guid}/share")]
        [Authorize]
        [ProducesResponseType(typeof(RouteShareTokenDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RouteShareTokenDto>> CreateShareToken(
            Guid id,
            [FromBody] CreateShareTokenRequest? request = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                _logger.LogInformation("Creating share token for route {RouteId} by user {UserId}", id, userId);

                var command = new CreateRouteShareTokenCommand
                {
                    RouteId = id,
                    UserId = userId.Value,
                    ExpiresAt = request?.ExpiresAt
                };

                var result = await _mediator.Send(command);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Route not found: {RouteId}", id);
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized share attempt for route {RouteId}", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating share token for route {RouteId}", id);
                return StatusCode(500, new { error = "An error occurred while creating the share token" });
            }
        }

        /// <summary>
        /// Access a shared route using a share token (read-only, no auth required)
        /// </summary>
        /// <param name="token">Share token</param>
        /// <returns>Route data (no private user info)</returns>
        [HttpGet("shared/{token}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(SharedRouteDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status410Gone)]
        public async Task<ActionResult<SharedRouteDto>> GetSharedRoute(string token)
        {
            try
            {
                _logger.LogInformation("Accessing shared route with token");

                var query = new GetSharedRouteQuery { Token = token };
                var result = await _mediator.Send(query);

                if (result == null)
                {
                    return NotFound(new { error = "Shared route not found or token invalid" });
                }

                if (result.IsExpired)
                {
                    return StatusCode(410, new { error = "Share link has expired" });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing shared route");
                return StatusCode(500, new { error = "An error occurred while accessing the shared route" });
            }
        }

        // ===== Route Reroll Endpoint =====

        /// <summary>
        /// Regenerate a route with variation while keeping similar constraints
        /// </summary>
        /// <param name="id">Route ID</param>
        /// <param name="request">Reroll options</param>
        /// <returns>New route with variation</returns>
        [HttpPost("{id:guid}/reroll")]
        [Authorize]
        [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RouteDto>> RerollRoute(
            Guid id,
            [FromBody] RerollRouteRequest? request = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                _logger.LogInformation("Rerolling route {RouteId} for user {UserId}", id, userId);

                var command = new RerollRouteCommand
                {
                    RouteId = id,
                    UserId = userId.Value,
                    VariationFactor = request?.VariationFactor ?? 0.5,
                    KeepStopCount = request?.KeepStopCount ?? true,
                    SaveRevisionBeforeReroll = request?.SaveRevision ?? true
                };

                var result = await _mediator.Send(command);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Route not found or cannot reroll: {RouteId}", id);
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized reroll attempt for route {RouteId}", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rerolling route {RouteId}", id);
                return StatusCode(500, new { error = "An error occurred while rerolling the route" });
            }
        }

        // ===== Route Revisions Endpoint =====

        /// <summary>
        /// Get all revisions (version history) for a route
        /// </summary>
        /// <param name="id">Route ID</param>
        /// <returns>List of revisions</returns>
        [HttpGet("{id:guid}/revisions")]
        [Authorize]
        [ProducesResponseType(typeof(List<RouteRevisionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<List<RouteRevisionDto>>> GetRouteRevisions(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "User authentication required" });
                }

                _logger.LogInformation("Getting revisions for route {RouteId} by user {UserId}", id, userId);

                var query = new GetRouteRevisionsQuery
                {
                    RouteId = id,
                    UserId = userId.Value
                };

                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Route not found: {RouteId}", id);
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized revision access for route {RouteId}", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting revisions for route {RouteId}", id);
                return StatusCode(500, new { error = "An error occurred while retrieving route revisions" });
            }
        }
    }

    // ===== Request DTOs for new endpoints =====

    /// <summary>
    /// Request DTO for creating a share token
    /// </summary>
    public class CreateShareTokenRequest
    {
        /// <summary>
        /// Optional expiration date for the share link
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Request DTO for rerolling a route
    /// </summary>
    public class RerollRouteRequest
    {
        /// <summary>
        /// How much variation to introduce (0.0 = minimal, 1.0 = maximum). Default: 0.5
        /// </summary>
        public double VariationFactor { get; set; } = 0.5;

        /// <summary>
        /// Whether to keep the same number of stops. Default: true
        /// </summary>
        public bool KeepStopCount { get; set; } = true;

        /// <summary>
        /// Whether to save the current route as a revision before rerolling. Default: true
        /// </summary>
        public bool SaveRevision { get; set; } = true;
    }
}
