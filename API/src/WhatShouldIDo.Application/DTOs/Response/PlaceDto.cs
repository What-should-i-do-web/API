namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// DTO representing a place/point of interest
    /// </summary>
    public class PlaceDto
    {
        /// <summary>
        /// Unique place identifier (from provider)
        /// </summary>
        public string PlaceId { get; set; } = string.Empty;

        /// <summary>
        /// Place name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Place description or summary
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Full address
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Latitude coordinate
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude coordinate
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Place types/categories (e.g., "restaurant", "museum")
        /// </summary>
        public List<string>? Types { get; set; }

        /// <summary>
        /// Average rating (0.0 to 5.0)
        /// </summary>
        public double? Rating { get; set; }

        /// <summary>
        /// Total number of ratings/reviews
        /// </summary>
        public int? UserRatingsTotal { get; set; }

        /// <summary>
        /// Price level indicator
        /// </summary>
        public string? PriceLevel { get; set; }

        /// <summary>
        /// Phone number
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Website URL
        /// </summary>
        public string? Website { get; set; }

        /// <summary>
        /// Opening hours text
        /// </summary>
        public string? OpeningHours { get; set; }

        /// <summary>
        /// Photos URLs
        /// </summary>
        public List<string>? Photos { get; set; }

        /// <summary>
        /// Distance from search point (in meters)
        /// </summary>
        public double? Distance { get; set; }

        /// <summary>
        /// Provider source (e.g., "Google", "OpenTripMap")
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
