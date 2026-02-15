using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Application.Services
{
    /// <summary>
    /// Policy service that enforces intent-based rules on suggestions.
    /// Ensures FOOD_ONLY doesn't include activities, ROUTE_PLANNING builds routes, etc.
    /// </summary>
    public interface ISuggestionPolicy
    {
        /// <summary>
        /// Validates that a request is compatible with the specified intent.
        /// Returns validation errors if intent requirements are not met.
        /// </summary>
        /// <param name="intent">User's intent</param>
        /// <param name="location">Location coordinates (lat, lng)</param>
        /// <param name="radiusMeters">Search radius</param>
        /// <param name="walkingDistance">Max walking distance for route planning</param>
        /// <returns>List of validation errors (empty if valid)</returns>
        Task<List<string>> ValidateRequestAsync(
            SuggestionIntent intent,
            (double latitude, double longitude) location,
            int radiusMeters,
            int? walkingDistance);

        /// <summary>
        /// Filters places to match intent constraints.
        /// FOOD_ONLY => only food categories, ACTIVITY_ONLY => only activity categories, etc.
        /// </summary>
        /// <param name="intent">User's intent</param>
        /// <param name="places">Places to filter</param>
        /// <param name="userExclusions">User-excluded place IDs</param>
        /// <returns>Filtered places matching intent policy</returns>
        Task<List<Place>> ApplyIntentFilterAsync(
            SuggestionIntent intent,
            List<Place> places,
            List<string> userExclusions);

        /// <summary>
        /// Determines if route building should occur for this intent.
        /// ROUTE_PLANNING => true, others => false
        /// </summary>
        /// <param name="intent">User's intent</param>
        /// <returns>True if route should be built</returns>
        bool ShouldBuildRoute(SuggestionIntent intent);

        /// <summary>
        /// Determines if diversity scoring should be emphasized.
        /// TRY_SOMETHING_NEW => high diversity, others => normal
        /// </summary>
        /// <param name="intent">User's intent</param>
        /// <returns>Diversity factor (0.0 = low, 1.0 = high)</returns>
        double GetDiversityFactor(SuggestionIntent intent);

        /// <summary>
        /// Gets the maximum walking distance for route planning based on intent.
        /// </summary>
        /// <param name="intent">User's intent</param>
        /// <param name="userPreference">User-specified preference (optional)</param>
        /// <returns>Max walking distance in meters</returns>
        int GetMaxWalkingDistance(SuggestionIntent intent, int? userPreference);

        /// <summary>
        /// Generates explainability reasons for why a place was suggested.
        /// </summary>
        /// <param name="intent">User's intent</param>
        /// <param name="place">The suggested place</param>
        /// <param name="userLocation">User's location</param>
        /// <param name="matchedPreferences">Matched user preferences</param>
        /// <param name="noveltyScore">Novelty score (0.0-1.0)</param>
        /// <param name="contextualReasons">Context-based reasons (weather, time)</param>
        /// <returns>List of human-readable reason strings</returns>
        Task<List<string>> GenerateReasonsAsync(
            SuggestionIntent intent,
            Place place,
            (double latitude, double longitude) userLocation,
            List<string> matchedPreferences,
            double noveltyScore,
            List<string> contextualReasons);
    }
}
