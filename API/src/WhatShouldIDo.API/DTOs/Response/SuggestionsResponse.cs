using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.API.DTOs.Response
{
    /// <summary>
    /// API Response DTO for intent-first suggestions.
    /// Format adapts based on intent - either suggestions list or route/day plan.
    /// </summary>
    public class SuggestionsResponse
    {
        /// <summary>
        /// The intent that was processed
        /// </summary>
        public SuggestionIntent Intent { get; set; }

        /// <summary>
        /// Whether results are personalized (requires authenticated user)
        /// </summary>
        public bool IsPersonalized { get; set; }

        /// <summary>
        /// User ID (if authenticated)
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// List of suggestions (populated for QUICK_SUGGESTION, FOOD_ONLY, ACTIVITY_ONLY, TRY_SOMETHING_NEW)
        /// Null for ROUTE_PLANNING intent (use Route/DayPlan instead)
        /// </summary>
        public List<SuggestionDto>? Suggestions { get; set; }

        /// <summary>
        /// Route/Day plan (populated for ROUTE_PLANNING intent)
        /// Null for other intents (use Suggestions instead)
        /// </summary>
        public RouteDto? Route { get; set; }

        /// <summary>
        /// Day plan with timing details (alternative to Route for ROUTE_PLANNING)
        /// </summary>
        public DayPlanDto? DayPlan { get; set; }

        /// <summary>
        /// Total number of results
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Applied filters summary
        /// </summary>
        public FilterSummary Filters { get; set; } = new FilterSummary();

        /// <summary>
        /// Metadata about the suggestion generation
        /// </summary>
        public SuggestionMetadata Metadata { get; set; } = new SuggestionMetadata();
    }

    /// <summary>
    /// Individual suggestion in the response
    /// </summary>
    public class SuggestionDto
    {
        public Guid Id { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public double Score { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSponsored { get; set; }
        public DateTime? SponsoredUntil { get; set; }
        public string? PhotoReference { get; set; }
        public string? PhotoUrl { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
    }

    /// <summary>
    /// Route information in the response
    /// </summary>
    public class RouteDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid UserId { get; set; }
        public double TotalDistance { get; set; }
        public double EstimatedDuration { get; set; }
        public int StopCount { get; set; }
        public string TransportationMode { get; set; } = string.Empty;
        public string RouteType { get; set; } = string.Empty;
        public IReadOnlyList<string> Tags { get; set; } = new List<string>();
        public bool IsPublic { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Day plan with detailed timing
    /// </summary>
    public class DayPlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public List<DayPlanStopDto> Stops { get; set; } = new List<DayPlanStopDto>();
        public int TotalDurationMinutes { get; set; }
        public double TotalDistanceMeters { get; set; }
    }

    /// <summary>
    /// Individual stop in a day plan
    /// </summary>
    public class DayPlanStopDto
    {
        public int Order { get; set; }
        public Guid PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public TimeSpan ArrivalTime { get; set; }
        public int DurationMinutes { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Summary of filters that were applied
    /// </summary>
    public class FilterSummary
    {
        public int RadiusMeters { get; set; }
        public int? WalkingDistanceMeters { get; set; }
        public string? BudgetLevel { get; set; }
        public List<string> IncludedCategories { get; set; } = new List<string>();
        public List<string> ExcludedCategories { get; set; } = new List<string>();
        public List<string> DietaryRestrictions { get; set; } = new List<string>();
        public bool AppliedVariety { get; set; }
        public bool AppliedContextual { get; set; }
    }

    /// <summary>
    /// Metadata about how suggestions were generated
    /// </summary>
    public class SuggestionMetadata
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string Source { get; set; } = "HybridOrchestrator";
        public double DiversityFactor { get; set; }
        public bool UsedAI { get; set; }
        public bool UsedPersonalization { get; set; }
        public bool UsedContextEngine { get; set; }
        public bool UsedVariabilityEngine { get; set; }
        public string? TimeOfDay { get; set; }
        public string? WeatherCondition { get; set; }
        public string? Season { get; set; }
    }
}
