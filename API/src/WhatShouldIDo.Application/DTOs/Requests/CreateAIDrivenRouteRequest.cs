using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request DTO for creating an AI-driven personalized route
    /// </summary>
    public class CreateAIDrivenRouteRequest
    {
        /// <summary>
        /// Center latitude for search
        /// </summary>
        [Required]
        [Range(-90, 90)]
        public float Latitude { get; set; }

        /// <summary>
        /// Center longitude for search
        /// </summary>
        [Required]
        [Range(-180, 180)]
        public float Longitude { get; set; }

        /// <summary>
        /// Optional location name
        /// </summary>
        public string? LocationName { get; set; }

        /// <summary>
        /// Search radius in kilometers
        /// </summary>
        [Range(1, 50)]
        public int RadiusKm { get; set; } = 10;

        /// <summary>
        /// Start time for the day plan (default: 9:00 AM)
        /// </summary>
        public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);

        /// <summary>
        /// End time for the day plan (default: 6:00 PM)
        /// </summary>
        public TimeSpan EndTime { get; set; } = new TimeSpan(18, 0, 0);

        /// <summary>
        /// Preferred activity categories
        /// </summary>
        public List<string>? PreferredCategories { get; set; }

        /// <summary>
        /// Categories to avoid
        /// </summary>
        public List<string>? AvoidedCategories { get; set; }

        /// <summary>
        /// Budget preference ("low", "medium", "high")
        /// </summary>
        public string? Budget { get; set; }

        /// <summary>
        /// Transportation mode ("walking", "driving", "public")
        /// </summary>
        public string? Transportation { get; set; }

        /// <summary>
        /// Whether to include meal recommendations
        /// </summary>
        public bool IncludeMeals { get; set; } = true;

        /// <summary>
        /// Special requests (e.g., "Family friendly", "Romantic")
        /// </summary>
        public string? SpecialRequests { get; set; }

        /// <summary>
        /// Diversity factor for ε-greedy algorithm (0.0-1.0)
        /// 0.0 = only familiar preferences (exploitation)
        /// 1.0 = only novel experiences (exploration)
        /// Default: 0.2 (20% exploration, 80% exploitation)
        /// </summary>
        [Range(0.0, 1.0)]
        public double DiversityFactor { get; set; } = 0.2;
    }
}
