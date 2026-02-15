using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MediatR;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.DTOs.AI;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Commands;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DayPlanController : ControllerBase
    {
        private readonly IDayPlanningService _dayPlanningService;
        private readonly IMediator _mediator;
        private readonly ILogger<DayPlanController> _logger;

        public DayPlanController(
            IDayPlanningService dayPlanningService,
            IMediator mediator,
            ILogger<DayPlanController> logger)
        {
            _dayPlanningService = dayPlanningService;
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Creates a comprehensive day plan based on location and preferences
        /// Combines historical, restaurant, and entertainment suggestions into a full day itinerary
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<DayPlanDto>> CreateDayPlan([FromBody] DayPlanRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                
                DayPlanDto dayPlan;
                if (userId.HasValue)
                {
                    // Create personalized day plan for authenticated users
                    dayPlan = await _dayPlanningService.CreatePersonalizedDayPlanAsync(userId.Value, request);
                }
                else
                {
                    // Create basic day plan for anonymous users
                    dayPlan = await _dayPlanningService.CreateDayPlanAsync(request);
                }

                _logger.LogInformation("Created day plan with {ActivityCount} activities for location ({Lat}, {Lng})", 
                    dayPlan.Activities.Count, request.Latitude, request.Longitude);

                return Ok(dayPlan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating day plan");
                return StatusCode(500, new { error = "Gün planı oluşturulurken bir hata oluştu" });
            }
        }

        /// <summary>
        /// Get pre-made sample day plans for a location
        /// Includes Cultural Explorer, Food Adventure, and Entertainment plans
        /// </summary>
        [HttpGet("samples")]
        public async Task<ActionResult<List<DayPlanDto>>> GetSamplePlans([FromQuery] float lat, [FromQuery] float lng)
        {
            try
            {
                var samplePlans = await _dayPlanningService.GetSampleDayPlansAsync(lat, lng);
                
                _logger.LogInformation("Retrieved {PlanCount} sample day plans for location ({Lat}, {Lng})", 
                    samplePlans.Count, lat, lng);

                return Ok(samplePlans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sample day plans");
                return StatusCode(500, new { error = "Örnek planlar yüklenirken bir hata oluştu" });
            }
        }

        /// <summary>
        /// Quick day plan endpoint - simplified version for immediate suggestions
        /// Perfect for "What should I do today?" scenarios
        /// </summary>
        [HttpGet("quick")]
        public async Task<ActionResult<DayPlanDto>> GetQuickDayPlan(
            [FromQuery] float lat, 
            [FromQuery] float lng,
            [FromQuery] string? budget = "medium",
            [FromQuery] int hours = 8)
        {
            try
            {
                var request = new DayPlanRequest
                {
                    Latitude = lat,
                    Longitude = lng,
                    Budget = budget,
                    StartTime = TimeSpan.FromHours(9), // 9 AM
                    EndTime = TimeSpan.FromHours(9 + hours), // 9 AM + hours
                    RadiusKm = 15,
                    IncludeMeals = true,
                    Transportation = "walking"
                };

                var userId = GetCurrentUserId();
                DayPlanDto dayPlan;

                if (userId.HasValue)
                {
                    dayPlan = await _dayPlanningService.CreatePersonalizedDayPlanAsync(userId.Value, request);
                }
                else
                {
                    dayPlan = await _dayPlanningService.CreateDayPlanAsync(request);
                }

                // Simplify the response for quick endpoint
                dayPlan.Title = "Bugün İçin Hızlı Plan";
                dayPlan.Description = "Bulunduğunuz konuma yakın aktivitelerle dolu bir gün";

                return Ok(dayPlan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quick day plan");
                return StatusCode(500, new { error = "Hızlı plan oluşturulurken bir hata oluştu" });
            }
        }

        /// <summary>
        /// Get category-specific day plans (historical, food, entertainment)
        /// </summary>
        [HttpPost("category/{category}")]
        public async Task<ActionResult<DayPlanDto>> CreateCategoryDayPlan(
            string category, 
            [FromBody] DayPlanRequest request)
        {
            try
            {
                // Override preferences with specific category
                var categoryRequest = request;
                categoryRequest.PreferredCategories = new List<string> { category.ToLower() };

                var userId = GetCurrentUserId();
                DayPlanDto dayPlan;

                if (userId.HasValue)
                {
                    dayPlan = await _dayPlanningService.CreatePersonalizedDayPlanAsync(userId.Value, categoryRequest);
                }
                else
                {
                    dayPlan = await _dayPlanningService.CreateDayPlanAsync(categoryRequest);
                }

                // Customize title based on category
                dayPlan.Title = category.ToLower() switch
                {
                    "historical" => "Tarih ve Kültür Turu",
                    "food" => "Lezzet Yolculuğu",
                    "entertainment" => "Eğlence Dolu Gün",
                    _ => $"{category.ToTitleCase()} Odaklı Plan"
                };

                return Ok(dayPlan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category day plan for {Category}", category);
                return StatusCode(500, new { error = $"{category} planı oluşturulurken bir hata oluştu" });
            }
        }

        /// <summary>
        /// Generate an AI-driven daily itinerary with intelligent place selection and timing
        /// Uses GPT-4 to create a personalized, optimized day plan based on preferences
        /// </summary>
        /// <remarks>
        /// This endpoint leverages AI to:
        /// - Intelligently select and order places based on your preferences
        /// - Optimize timing and transportation between stops
        /// - Balance activity types (sightseeing, meals, breaks)
        /// - Consider budget, dietary restrictions, and accessibility
        ///
        /// Example request:
        ///
        ///     POST /api/dayplan/ai-generate
        ///     {
        ///       "location": "Istanbul, Turkey",
        ///       "latitude": 41.0082,
        ///       "longitude": 28.9784,
        ///       "startTime": "09:00:00",
        ///       "endTime": "20:00:00",
        ///       "preferredActivities": ["cultural", "food", "shopping"],
        ///       "budgetLevel": "medium",
        ///       "maxStops": 6,
        ///       "transportationMode": "walking",
        ///       "saveAsRoute": true
        ///     }
        ///
        /// </remarks>
        /// <param name="request">Itinerary generation request</param>
        /// <returns>AI-generated itinerary with detailed stops and reasoning</returns>
        /// <response code="200">Returns the generated itinerary</response>
        /// <response code="400">If the request is invalid</response>
        /// <response code="401">If user is not authenticated</response>
        /// <response code="500">If AI generation fails</response>
        [HttpPost("ai-generate")]
        [Authorize]
        [ProducesResponseType(typeof(AIItinerary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AIItinerary>> GenerateAIItinerary([FromBody] AIItineraryRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (!userId.HasValue)
                {
                    return Unauthorized(new { error = "Authentication required for AI-driven itinerary generation" });
                }

                _logger.LogInformation("Generating AI itinerary for user {UserId} at {Location}",
                    userId, request.Location);

                // Create command
                var command = new GenerateDailyItineraryCommand
                {
                    UserId = userId.Value,
                    Location = request.Location,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    TargetDate = request.TargetDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    PreferredActivities = request.PreferredActivities,
                    DietaryPreferences = request.DietaryPreferences,
                    BudgetLevel = request.BudgetLevel,
                    RadiusMeters = request.RadiusMeters,
                    MaxStops = request.MaxStops,
                    TransportationMode = request.TransportationMode,
                    AdditionalPreferences = request.AdditionalPreferences,
                    SaveAsRoute = true // Always save AI-generated itineraries as routes
                };

                // Execute via MediatR
                var itinerary = await _mediator.Send(command);

                _logger.LogInformation("Successfully generated AI itinerary with {StopCount} stops: {Title}",
                    itinerary.Stops.Count, itinerary.Title);

                return Ok(itinerary);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to generate AI itinerary: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI-driven itinerary");
                return StatusCode(500, new { error = "AI-driven itinerary generation failed. Please try again." });
            }
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                             User.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }
    }
}