namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request to submit feedback on a recommended place.
    /// Used for incremental taste profile evolution.
    /// </summary>
    public class PlaceFeedbackRequest
    {
        /// <summary>
        /// Place identifier (from provider).
        /// </summary>
        public string PlaceId { get; set; } = string.Empty;

        /// <summary>
        /// Place provider/source (e.g., "google", "opentripmap").
        /// </summary>
        public string Provider { get; set; } = "google";

        /// <summary>
        /// Feedback type: "like", "dislike", "skip".
        /// </summary>
        public string FeedbackType { get; set; } = string.Empty;

        /// <summary>
        /// Optional reason code for why user gave this feedback.
        /// </summary>
        public string? ReasonCode { get; set; }

        /// <summary>
        /// Optional session identifier to group related feedback.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Place category/type for delta calculation.
        /// </summary>
        public string PlaceCategory { get; set; } = string.Empty;
    }
}
