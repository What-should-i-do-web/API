using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request for generating a "Surprise Me" personalized route
    /// </summary>
    public class SurpriseMeRequest
    {
        /// <summary>
        /// Target area name (e.g., "Istanbul", "Kadıköy")
        /// </summary>
        [Required]
        public string TargetArea { get; set; } = string.Empty;

        /// <summary>
        /// Latitude of the target area center
        /// </summary>
        [Range(-90, 90)]
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude of the target area center
        /// </summary>
        [Range(-180, 180)]
        public double Longitude { get; set; }

        /// <summary>
        /// Search radius in meters (default: 5000m = 5km)
        /// </summary>
        [Range(500, 50000)]
        public int RadiusMeters { get; set; } = 5000;

        /// <summary>
        /// Optional time window start (e.g., "09:00")
        /// </summary>
        public string? TimeWindowStart { get; set; }

        /// <summary>
        /// Optional time window end (e.g., "18:00")
        /// </summary>
        public string? TimeWindowEnd { get; set; }

        /// <summary>
        /// If true, generates a full-day itinerary (ignores time window)
        /// </summary>
        public bool IsFullDay { get; set; } = false;

        /// <summary>
        /// Preferred activity categories (e.g., "museum", "restaurant", "park")
        /// </summary>
        public List<string>? PreferredCategories { get; set; }

        /// <summary>
        /// Budget level: "low", "medium", "high"
        /// </summary>
        public string? BudgetLevel { get; set; }

        /// <summary>
        /// Minimum number of stops (default: 3)
        /// </summary>
        [Range(2, 10)]
        public int MinStops { get; set; } = 3;

        /// <summary>
        /// Maximum number of stops (default: 6)
        /// </summary>
        [Range(2, 10)]
        public int MaxStops { get; set; } = 6;

        /// <summary>
        /// Transportation mode: "walking", "driving", "transit"
        /// </summary>
        public string TransportationMode { get; set; } = "walking";

        /// <summary>
        /// If true, includes AI reasoning for each suggestion
        /// </summary>
        public bool IncludeReasoning { get; set; } = true;

        /// <summary>
        /// If true, saves the generated route to user's history
        /// </summary>
        public bool SaveToHistory { get; set; } = true;
    }
}
