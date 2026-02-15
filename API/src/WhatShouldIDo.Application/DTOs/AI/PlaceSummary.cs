namespace WhatShouldIDo.Application.DTOs.AI
{
    /// <summary>
    /// AI-generated summary of a place including highlights, sentiment, and key features.
    /// </summary>
    public class PlaceSummary
    {
        /// <summary>
        /// Place identifier
        /// </summary>
        public string PlaceId { get; set; } = string.Empty;

        /// <summary>
        /// Concise AI-generated summary (1-3 sentences)
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Key highlights or features (e.g., "Great coffee", "Beautiful view", "Affordable")
        /// </summary>
        public List<string> Highlights { get; set; } = new();

        /// <summary>
        /// Overall sentiment score (0.0 = very negative, 1.0 = very positive)
        /// </summary>
        public double SentimentScore { get; set; }

        /// <summary>
        /// Best suited for (e.g., "families", "couples", "solo travelers")
        /// </summary>
        public List<string> BestFor { get; set; } = new();

        /// <summary>
        /// Recommended time to visit (e.g., "morning", "sunset", "weekday evenings")
        /// </summary>
        public string? RecommendedTime { get; set; }
    }
}
