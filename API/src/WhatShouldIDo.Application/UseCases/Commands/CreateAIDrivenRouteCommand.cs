using MediatR;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.UseCases.Commands
{
    /// <summary>
    /// Command for creating an AI-driven personalized day plan route
    /// Uses user embeddings and diversity algorithms to balance familiar and novel experiences
    /// </summary>
    public class CreateAIDrivenRouteCommand : IRequest<DayPlanDto>
    {
        /// <summary>
        /// User ID for personalization
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Center latitude for search
        /// </summary>
        public float Latitude { get; set; }

        /// <summary>
        /// Center longitude for search
        /// </summary>
        public float Longitude { get; set; }

        /// <summary>
        /// Optional location name
        /// </summary>
        public string? LocationName { get; set; }

        /// <summary>
        /// Search radius in kilometers
        /// </summary>
        public int RadiusKm { get; set; } = 10;

        /// <summary>
        /// Start time for the day plan
        /// </summary>
        public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0); // 9:00 AM

        /// <summary>
        /// End time for the day plan
        /// </summary>
        public TimeSpan EndTime { get; set; } = new TimeSpan(18, 0, 0); // 6:00 PM

        /// <summary>
        /// Preferred categories for activities
        /// </summary>
        public List<string> PreferredCategories { get; set; } = new();

        /// <summary>
        /// Categories to avoid
        /// </summary>
        public List<string> AvoidedCategories { get; set; } = new();

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
        /// Default: 0.2 (20% exploration)
        /// </summary>
        public double DiversityFactor { get; set; } = 0.2;
    }
}
