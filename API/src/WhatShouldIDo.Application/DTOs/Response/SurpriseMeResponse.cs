namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// Response for "Surprise Me" personalized route generation
    /// </summary>
    public class SurpriseMeResponse
    {
        /// <summary>
        /// The generated route with optimized order
        /// </summary>
        public RouteDto Route { get; set; } = null!;

        /// <summary>
        /// Suggested places with personalization metadata
        /// </summary>
        public List<SurpriseMePlaceDto> SuggestedPlaces { get; set; } = new();

        /// <summary>
        /// AI-generated reasoning for the route
        /// </summary>
        public string? Reasoning { get; set; }

        /// <summary>
        /// Diversity score (0-1, higher = more diverse categories)
        /// </summary>
        public double DiversityScore { get; set; }

        /// <summary>
        /// Personalization score (0-1, higher = better match to preferences)
        /// </summary>
        public double PersonalizationScore { get; set; }

        /// <summary>
        /// Session ID for tracking this suggestion batch
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Whether this route was saved to history
        /// </summary>
        public bool SavedToHistory { get; set; }
    }

    /// <summary>
    /// Place suggestion with personalization metadata
    /// </summary>
    public class SurpriseMePlaceDto
    {
        public string PlaceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Address { get; set; }
        public double? Rating { get; set; }
        public int? PriceLevel { get; set; }
        public string? PhotoUrl { get; set; }

        /// <summary>
        /// Order in the optimized route (1-based)
        /// </summary>
        public int RouteOrder { get; set; }

        /// <summary>
        /// AI-generated reason for this suggestion
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Personalization score (0-1)
        /// </summary>
        public double PersonalizationScore { get; set; }

        /// <summary>
        /// True if this place is in user's favorites
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// True if user has visited this place before
        /// </summary>
        public bool PreviouslyVisited { get; set; }

        /// <summary>
        /// Distance from previous stop (in meters), null for first stop
        /// </summary>
        public double? DistanceFromPrevious { get; set; }

        /// <summary>
        /// Estimated travel time from previous stop (in minutes), null for first stop
        /// </summary>
        public int? TravelTimeFromPrevious { get; set; }
    }
}
