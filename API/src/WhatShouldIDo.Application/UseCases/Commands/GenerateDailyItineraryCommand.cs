using MediatR;
using WhatShouldIDo.Application.DTOs.AI;

namespace WhatShouldIDo.Application.UseCases.Commands
{
    /// <summary>
    /// Command for generating an AI-driven daily itinerary
    /// </summary>
    public class GenerateDailyItineraryCommand : IRequest<AIItinerary>
    {
        /// <summary>
        /// User ID (for personalization)
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Target city or location name
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Center point latitude
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Center point longitude
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Target date for the itinerary (optional, defaults to today)
        /// </summary>
        public DateTime? TargetDate { get; set; }

        /// <summary>
        /// Start time for the day (default: 09:00)
        /// </summary>
        public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);

        /// <summary>
        /// End time for the day (default: 22:00)
        /// </summary>
        public TimeSpan EndTime { get; set; } = new TimeSpan(22, 0, 0);

        /// <summary>
        /// Preferred activity types (e.g., "cultural", "outdoor", "food")
        /// </summary>
        public List<string> PreferredActivities { get; set; } = new();

        /// <summary>
        /// Dietary preferences or restrictions
        /// </summary>
        public List<string> DietaryPreferences { get; set; } = new();

        /// <summary>
        /// Budget level ("low", "medium", "high")
        /// </summary>
        public string BudgetLevel { get; set; } = "medium";

        /// <summary>
        /// Search radius in meters (default: 5000)
        /// </summary>
        public int RadiusMeters { get; set; } = 5000;

        /// <summary>
        /// Maximum number of stops in the itinerary
        /// </summary>
        public int MaxStops { get; set; } = 6;

        /// <summary>
        /// Transportation mode
        /// </summary>
        public string TransportationMode { get; set; } = "walking";

        /// <summary>
        /// Additional free-text preferences
        /// </summary>
        public string? AdditionalPreferences { get; set; }

        /// <summary>
        /// Whether to save the generated itinerary as a route
        /// </summary>
        public bool SaveAsRoute { get; set; } = true;
    }
}
