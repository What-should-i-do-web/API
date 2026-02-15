namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// Represents a single reason why a place was recommended.
    /// Provides explainability for recommendation scoring.
    /// </summary>
    public class RecommendationReasonDto
    {
        /// <summary>
        /// Stable reason code (language-independent).
        /// Examples: "MATCHES_INTEREST_FOOD", "HIGHLY_RATED", "TRY_SOMETHING_NEW", "CLOSE_TO_YOU"
        /// </summary>
        public string ReasonCode { get; set; } = string.Empty;

        /// <summary>
        /// Localized human-readable message.
        /// Example (en): "Matches your food interest"
        /// Example (tr): "Yemek ilgi alanınıza uyuyor"
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Score contribution weight (0-1) for this reason.
        /// Only included when EnableDebugFields is true in configuration.
        /// </summary>
        public double? Weight { get; set; }
    }
}
