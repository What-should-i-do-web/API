using System.Collections.Generic;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Models;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for generating human-readable explanations of recommendation scores.
    /// Converts score components into localized reasons.
    /// </summary>
    public interface IExplainabilityService
    {
        /// <summary>
        /// Generate recommendation reasons from score breakdown.
        /// Returns 2-4 top reasons explaining why the place was recommended.
        /// Reasons are localized based on current culture.
        /// </summary>
        /// <param name="place">The recommended place.</param>
        /// <param name="scoreBreakdown">Detailed score components.</param>
        /// <param name="context">Scoring context.</param>
        /// <returns>List of localized reasons (2-4 items).</returns>
        List<RecommendationReasonDto> GenerateReasons(
            Place place,
            ScoreBreakdown scoreBreakdown,
            ScoringContext context);

        /// <summary>
        /// Get localized reason message for a reason code.
        /// </summary>
        /// <param name="reasonCode">Stable reason code (e.g., "MATCHES_INTEREST_FOOD").</param>
        /// <returns>Localized message.</returns>
        string GetReasonMessage(string reasonCode);

        /// <summary>
        /// Get all available reason codes with their default messages (for documentation).
        /// </summary>
        Dictionary<string, string> GetAllReasonCodes();
    }
}
