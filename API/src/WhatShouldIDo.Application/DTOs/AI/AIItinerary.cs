using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.DTOs.AI
{
    /// <summary>
    /// AI-generated daily itinerary with ordered stops, timing, and reasoning
    /// </summary>
    public class AIItinerary
    {
        /// <summary>
        /// Unique identifier for this itinerary
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Title or name of the itinerary
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// AI-generated description or overview
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Target date for this itinerary
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Ordered list of stops in the itinerary
        /// </summary>
        public List<ItineraryStop> Stops { get; set; } = new();

        /// <summary>
        /// Total estimated duration in minutes
        /// </summary>
        public int TotalDurationMinutes { get; set; }

        /// <summary>
        /// Total estimated travel distance in meters
        /// </summary>
        public int TotalDistanceMeters { get; set; }

        /// <summary>
        /// AI reasoning or explanation for this itinerary
        /// </summary>
        public string Reasoning { get; set; } = string.Empty;

        /// <summary>
        /// Recommended transportation mode
        /// </summary>
        public string TransportationMode { get; set; } = "walking";

        /// <summary>
        /// Estimated total cost range (if applicable)
        /// </summary>
        public string? EstimatedCost { get; set; }

        /// <summary>
        /// When this itinerary was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// A single stop in the itinerary
    /// </summary>
    public class ItineraryStop
    {
        /// <summary>
        /// Order in the sequence (1-based)
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Place information
        /// </summary>
        public PlaceDto Place { get; set; } = null!;

        /// <summary>
        /// Suggested arrival time
        /// </summary>
        public TimeSpan ArrivalTime { get; set; }

        /// <summary>
        /// Suggested duration at this location (minutes)
        /// </summary>
        public int DurationMinutes { get; set; }

        /// <summary>
        /// Activity type (e.g., "breakfast", "sightseeing", "lunch", "coffee break")
        /// </summary>
        public string ActivityType { get; set; } = string.Empty;

        /// <summary>
        /// AI reasoning for including this stop
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Travel time from previous stop (minutes)
        /// </summary>
        public int? TravelTimeFromPrevious { get; set; }

        /// <summary>
        /// Distance from previous stop (meters)
        /// </summary>
        public int? DistanceFromPrevious { get; set; }
    }
}
