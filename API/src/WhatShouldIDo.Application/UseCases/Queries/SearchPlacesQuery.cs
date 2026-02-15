using MediatR;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.UseCases.Queries
{
    /// <summary>
    /// Query for searching places with AI-powered prompt interpretation
    /// </summary>
    public class SearchPlacesQuery : IRequest<SearchPlacesResult>
    {
        /// <summary>
        /// Natural language search query or structured filters
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Center latitude for search
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Center longitude for search
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Search radius in meters (default: 5000)
        /// </summary>
        public int Radius { get; set; } = 5000;

        /// <summary>
        /// Maximum number of results
        /// </summary>
        public int MaxResults { get; set; } = 20;

        /// <summary>
        /// Whether to use AI for semantic ranking
        /// </summary>
        public bool UseAIRanking { get; set; } = true;

        /// <summary>
        /// Optional place types filter (e.g., "restaurant", "museum")
        /// </summary>
        public List<string>? PlaceTypes { get; set; }

        /// <summary>
        /// Optional price level filter
        /// </summary>
        public string? PriceLevel { get; set; }

        /// <summary>
        /// User ID for personalization (optional)
        /// </summary>
        public Guid? UserId { get; set; }
    }

    public class SearchPlacesResult
    {
        public List<PlaceDto> Places { get; set; } = new();
        public int TotalCount { get; set; }
        public string? InterpretedQuery { get; set; }
        public List<string> ExtractedCategories { get; set; } = new();
        public bool UsedAI { get; set; }
        public double? AIConfidence { get; set; }
    }
}
