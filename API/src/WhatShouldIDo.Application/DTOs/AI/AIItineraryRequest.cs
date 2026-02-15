namespace WhatShouldIDo.Application.DTOs.AI
{
    /// <summary>
    /// Request for AI-driven daily itinerary generation
    /// </summary>
    public class AIItineraryRequest
    {
        /// <summary>
        /// User ID (for personalization based on history)
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
        /// Target date for the itinerary
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
        /// Preferred activity types (e.g., "cultural", "outdoor", "food", "shopping")
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
        /// Maximum number of stops in the itinerary (default: 6)
        /// </summary>
        public int MaxStops { get; set; } = 6;

        /// <summary>
        /// Transportation mode ("walking", "driving", "transit")
        /// </summary>
        public string TransportationMode { get; set; } = "walking";

        /// <summary>
        /// Additional free-text preferences or constraints
        /// </summary>
        public string? AdditionalPreferences { get; set; }
    }
}
