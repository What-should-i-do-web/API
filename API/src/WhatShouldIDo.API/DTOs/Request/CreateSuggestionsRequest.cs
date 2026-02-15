using System.ComponentModel.DataAnnotations;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.API.DTOs.Request
{
    /// <summary>
    /// API Request DTO for intent-first suggestion orchestration.
    /// Unified endpoint that handles different user intents with appropriate orchestration.
    /// </summary>
    public class CreateSuggestionsRequest
    {
        /// <summary>
        /// User's intent - drives orchestration logic
        /// Required field that determines response format and filtering
        /// </summary>
        [Required]
        public SuggestionIntent Intent { get; set; }

        /// <summary>
        /// User location latitude
        /// </summary>
        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }

        /// <summary>
        /// User location longitude
        /// </summary>
        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }

        /// <summary>
        /// Optional area/location name for context (e.g., "Kadıköy", "Taksim")
        /// </summary>
        public string? AreaName { get; set; }

        /// <summary>
        /// Search radius in meters (default: 3000)
        /// Max: 50000 meters (50 km)
        /// </summary>
        [Range(100, 50000)]
        public int RadiusMeters { get; set; } = 3000;

        /// <summary>
        /// Maximum walking distance for route planning (meters)
        /// Required for ROUTE_PLANNING intent
        /// </summary>
        [Range(500, 10000)]
        public int? WalkingDistanceMeters { get; set; }

        /// <summary>
        /// Budget preference (FREE, INEXPENSIVE, MODERATE, EXPENSIVE, VERY_EXPENSIVE)
        /// </summary>
        public string? BudgetLevel { get; set; }

        /// <summary>
        /// Categories to include (optional filter)
        /// </summary>
        public List<string>? IncludeCategories { get; set; }

        /// <summary>
        /// Categories to exclude (optional filter)
        /// </summary>
        public List<string>? ExcludeCategories { get; set; }

        /// <summary>
        /// Dietary restrictions (e.g., "vegetarian", "vegan", "halal", "gluten-free")
        /// </summary>
        public List<string>? DietaryRestrictions { get; set; }

        /// <summary>
        /// User preferences from onboarding if profile not yet established
        /// Format: ["outdoor", "cultural", "foodie", "active", etc.]
        /// </summary>
        public List<string>? OnboardingPreferences { get; set; }

        /// <summary>
        /// Time of day context (morning, afternoon, evening, night)
        /// Auto-detected if not provided
        /// </summary>
        public string? TimeOfDay { get; set; }

        /// <summary>
        /// Internal: Authenticated user ID
        /// Set by controller from JWT claims - not from request body
        /// </summary>
        internal Guid? UserId { get; set; }
    }
}
