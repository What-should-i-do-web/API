using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhatShouldIDo.API.DTOs.Request;
using WhatShouldIDo.API.DTOs.Response;
using WhatShouldIDo.Application.Models;
using WhatShouldIDo.Application.UseCases.Commands;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.API.Controllers
{
    /// <summary>
    /// Intent-first suggestion orchestration endpoint.
    /// Unified API for all suggestion intents (FOOD_ONLY, ROUTE_PLANNING, TRY_SOMETHING_NEW, etc.)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SuggestionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<SuggestionsController> _logger;

        public SuggestionsController(
            IMediator mediator,
            ILogger<SuggestionsController> logger)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Create intent-based suggestions with optional personalization.
        /// This is the primary entry point for the new intent-first suggestion system.
        /// </summary>
        /// <param name="request">Suggestion request with intent and context</param>
        /// <returns>
        /// For FOOD_ONLY, ACTIVITY_ONLY, QUICK_SUGGESTION, TRY_SOMETHING_NEW: Returns list of suggestions
        /// For ROUTE_PLANNING: Returns a route/day plan with optimized stops
        /// </returns>
        /// <response code="200">Suggestions created successfully</response>
        /// <response code="400">Invalid request (validation failed)</response>
        /// <response code="401">Unauthorized (if premium-only intent without auth)</response>
        /// <response code="403">Forbidden (quota exceeded)</response>
        /// <response code="500">Internal server error</response>
        [HttpPost]
        [AllowAnonymous] // Allow both authenticated and anonymous (personalization happens if authenticated)
        [ProducesResponseType(typeof(SuggestionsResponse), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> CreateSuggestions([FromBody] CreateSuggestionsRequest request)
        {
            try
            {
                // Extract user ID from claims if authenticated
                Guid? userId = null;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

                if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
                {
                    userId = parsedUserId;
                    _logger.LogInformation("Processing suggestions request for authenticated user {UserId} with intent {Intent}",
                        userId, request.Intent);
                }
                else
                {
                    _logger.LogInformation("Processing suggestions request for anonymous user with intent {Intent}", request.Intent);
                }

                // Map API DTO to Application Input model
                var input = MapToInput(request, userId);

                // Execute command via MediatR
                var command = new CreateSuggestionsCommand(input);
                var result = await _mediator.Send(command);

                // Map Application Result to API Response DTO
                var response = MapToResponse(result);

                _logger.LogInformation("Suggestions created successfully: Intent={Intent}, Count={Count}, Personalized={Personalized}",
                    response.Intent, response.TotalCount, response.IsPersonalized);

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid suggestion request: {Message}", ex.Message);
                return BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating suggestions for intent {Intent}", request.Intent);
                return StatusCode(500, new ProblemDetails
                {
                    Status = 500,
                    Title = "Internal Server Error",
                    Detail = "An error occurred while creating suggestions. Please try again.",
                    Instance = HttpContext.Request.Path
                });
            }
        }

        /// <summary>
        /// Get available suggestion intents and their descriptions.
        /// Useful for frontend to display intent options to users.
        /// </summary>
        /// <returns>List of available intents with metadata</returns>
        [HttpGet("intents")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<IntentInfo>), 200)]
        public IActionResult GetAvailableIntents()
        {
            var intents = new List<IntentInfo>
            {
                new IntentInfo
                {
                    Value = SuggestionIntent.QUICK_SUGGESTION,
                    DisplayName = "Quick Suggestion",
                    Description = "Get a few quick suggestions for immediate decision",
                    MaxResults = 3,
                    RequiresRoute = false
                },
                new IntentInfo
                {
                    Value = SuggestionIntent.FOOD_ONLY,
                    DisplayName = "Food & Dining",
                    Description = "Restaurants, cafes, and dining options only",
                    MaxResults = 10,
                    RequiresRoute = false
                },
                new IntentInfo
                {
                    Value = SuggestionIntent.ACTIVITY_ONLY,
                    DisplayName = "Activities & Entertainment",
                    Description = "Fun activities, entertainment, and cultural experiences",
                    MaxResults = 10,
                    RequiresRoute = false
                },
                new IntentInfo
                {
                    Value = SuggestionIntent.ROUTE_PLANNING,
                    DisplayName = "Day Plan / Route",
                    Description = "Multi-stop day plan with optimized route",
                    MaxResults = 8,
                    RequiresRoute = true
                },
                new IntentInfo
                {
                    Value = SuggestionIntent.TRY_SOMETHING_NEW,
                    DisplayName = "Try Something New",
                    Description = "Discover novel experiences based on your preferences",
                    MaxResults = 5,
                    RequiresRoute = false
                }
            };

            return Ok(intents);
        }

        #region Mapping Methods

        private static CreateSuggestionsInput MapToInput(CreateSuggestionsRequest request, Guid? userId)
        {
            return new CreateSuggestionsInput(
                Intent: request.Intent,
                Latitude: request.Latitude,
                Longitude: request.Longitude,
                RadiusMeters: request.RadiusMeters,
                AreaName: request.AreaName,
                WalkingDistanceMeters: request.WalkingDistanceMeters,
                BudgetLevel: request.BudgetLevel,
                IncludeCategories: request.IncludeCategories,
                ExcludeCategories: request.ExcludeCategories,
                DietaryRestrictions: request.DietaryRestrictions,
                OnboardingPreferences: request.OnboardingPreferences,
                TimeOfDay: request.TimeOfDay,
                UserId: userId
            );
        }

        private static SuggestionsResponse MapToResponse(SuggestionsResult result)
        {
            return new SuggestionsResponse
            {
                Intent = result.Intent,
                IsPersonalized = result.IsPersonalized,
                UserId = result.UserId,
                Suggestions = result.Suggestions?.Select(s => new DTOs.Response.SuggestionDto
                {
                    Id = s.Id,
                    PlaceName = s.PlaceName,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Category = s.Category,
                    Source = s.Source,
                    Reason = s.Reason,
                    Score = s.Score,
                    CreatedAt = s.CreatedAt,
                    IsSponsored = s.IsSponsored,
                    SponsoredUntil = s.SponsoredUntil,
                    PhotoReference = s.PhotoReference,
                    PhotoUrl = s.PhotoUrl,
                    Reasons = s.Reasons?.ToList() ?? new List<string>()
                }).ToList(),
                Route = result.Route != null ? new DTOs.Response.RouteDto
                {
                    Id = result.Route.Id,
                    Name = result.Route.Name,
                    Description = result.Route.Description,
                    UserId = result.Route.UserId,
                    TotalDistance = result.Route.TotalDistance,
                    EstimatedDuration = result.Route.EstimatedDuration,
                    StopCount = result.Route.StopCount,
                    TransportationMode = result.Route.TransportationMode,
                    RouteType = result.Route.RouteType,
                    Tags = result.Route.Tags,
                    IsPublic = result.Route.IsPublic,
                    CreatedAt = result.Route.CreatedAt,
                    UpdatedAt = result.Route.UpdatedAt
                } : null,
                DayPlan = result.DayPlan != null ? new DTOs.Response.DayPlanDto
                {
                    Id = result.DayPlan.Id,
                    Name = result.DayPlan.Name,
                    Date = result.DayPlan.Date,
                    Stops = result.DayPlan.Stops?.Select(stop => new DayPlanStopDto
                    {
                        Order = stop.Order,
                        PlaceId = stop.PlaceId,
                        PlaceName = stop.PlaceName,
                        ArrivalTime = stop.ArrivalTime,
                        DurationMinutes = stop.DurationMinutes,
                        Notes = stop.Notes
                    }).ToList() ?? new List<DayPlanStopDto>(),
                    TotalDurationMinutes = result.DayPlan.TotalDurationMinutes,
                    TotalDistanceMeters = result.DayPlan.TotalDistanceMeters
                } : null,
                TotalCount = result.TotalCount,
                Filters = new FilterSummary
                {
                    RadiusMeters = result.Filters.RadiusMeters,
                    WalkingDistanceMeters = result.Filters.WalkingDistanceMeters,
                    BudgetLevel = result.Filters.BudgetLevel,
                    IncludedCategories = result.Filters.IncludedCategories?.ToList() ?? new List<string>(),
                    ExcludedCategories = result.Filters.ExcludedCategories?.ToList() ?? new List<string>(),
                    DietaryRestrictions = result.Filters.DietaryRestrictions?.ToList() ?? new List<string>(),
                    AppliedVariety = result.Filters.AppliedVariety,
                    AppliedContextual = result.Filters.AppliedContextual
                },
                Metadata = new SuggestionMetadata
                {
                    GeneratedAt = result.Metadata.GeneratedAt,
                    Source = result.Metadata.Source,
                    DiversityFactor = result.Metadata.DiversityFactor,
                    UsedAI = result.Metadata.UsedAI,
                    UsedPersonalization = result.Metadata.UsedPersonalization,
                    UsedContextEngine = result.Metadata.UsedContextEngine,
                    UsedVariabilityEngine = result.Metadata.UsedVariabilityEngine,
                    TimeOfDay = result.Metadata.TimeOfDay,
                    WeatherCondition = result.Metadata.WeatherCondition,
                    Season = result.Metadata.Season
                }
            };
        }

        #endregion

        /// <summary>
        /// Intent metadata for frontend consumption
        /// </summary>
        public class IntentInfo
        {
            public SuggestionIntent Value { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int MaxResults { get; set; }
            public bool RequiresRoute { get; set; }
        }
    }
}
