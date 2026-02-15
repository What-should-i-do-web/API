using MediatR;

namespace WhatShouldIDo.Application.UseCases.Queries
{
    /// <summary>
    /// Query for getting an AI-generated summary of a place
    /// </summary>
    public class GetPlaceSummaryQuery : IRequest<PlaceSummaryResult>
    {
        /// <summary>
        /// Place ID from provider (Google, OpenTripMap, etc.)
        /// </summary>
        public string PlaceId { get; set; } = string.Empty;

        /// <summary>
        /// Optional user ID for personalized summary
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Summary style ("brief", "detailed", "highlights")
        /// Default: "brief"
        /// </summary>
        public string Style { get; set; } = "brief";
    }

    /// <summary>
    /// Result containing AI-generated place summary
    /// </summary>
    public class PlaceSummaryResult
    {
        /// <summary>
        /// Place ID
        /// </summary>
        public string PlaceId { get; set; } = string.Empty;

        /// <summary>
        /// Place name
        /// </summary>
        public string PlaceName { get; set; } = string.Empty;

        /// <summary>
        /// AI-generated summary text
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Key highlights extracted by AI (bullet points)
        /// </summary>
        public List<string> Highlights { get; set; } = new();

        /// <summary>
        /// Best time to visit suggestion
        /// </summary>
        public string? BestTimeToVisit { get; set; }

        /// <summary>
        /// Recommended duration for visit
        /// </summary>
        public string? RecommendedDuration { get; set; }

        /// <summary>
        /// Who this place is best for (families, couples, solo travelers, etc.)
        /// </summary>
        public List<string> BestFor { get; set; } = new();

        /// <summary>
        /// Whether AI was used to generate summary
        /// </summary>
        public bool UsedAI { get; set; }

        /// <summary>
        /// AI provider used
        /// </summary>
        public string? AIProvider { get; set; }

        /// <summary>
        /// Raw place data
        /// </summary>
        public Dictionary<string, object>? PlaceData { get; set; }
    }
}
