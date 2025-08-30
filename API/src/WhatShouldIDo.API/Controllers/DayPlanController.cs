using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DayPlanController : ControllerBase
    {
        private readonly IDayPlanningService _dayPlanningService;
        private readonly ILogger<DayPlanController> _logger;

        public DayPlanController(IDayPlanningService dayPlanningService, ILogger<DayPlanController> logger)
        {
            _dayPlanningService = dayPlanningService;
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