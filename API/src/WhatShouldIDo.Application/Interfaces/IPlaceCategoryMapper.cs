using System.Collections.Generic;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Maps place categories/types from providers (Google Places, OpenTripMap)
    /// to taste profile interest dimensions.
    /// This is foundational for explicit scoring.
    /// </summary>
    public interface IPlaceCategoryMapper
    {
        /// <summary>
        /// Map a place category/type string to interest dimensions with weights.
        /// Returns dictionary of interest → weight (0-1).
        /// A place can map to multiple interests (e.g., "restaurant" → Food:1.0, Culture:0.3).
        /// </summary>
        /// <param name="category">Place category or type (e.g., "restaurant", "museum", "park").</param>
        /// <returns>Dictionary of interest dimension → weight.</returns>
        Dictionary<string, double> MapToInterests(string category);

        /// <summary>
        /// Get dominant interest for a category (highest weight).
        /// Returns null if category is not recognized.
        /// </summary>
        /// <param name="category">Place category or type.</param>
        /// <returns>Dominant interest name (e.g., "Food", "Culture") or null.</returns>
        string? GetDominantInterest(string category);

        /// <summary>
        /// Check if a category is recognized by the mapper.
        /// </summary>
        bool IsRecognizedCategory(string category);

        /// <summary>
        /// Get all supported interest dimensions.
        /// Returns: ["Culture", "Food", "Nature", "Nightlife", "Shopping", "Art", "Wellness", "Sports"]
        /// </summary>
        List<string> GetAllInterests();

        /// <summary>
        /// Get example categories for an interest (for debugging/documentation).
        /// </summary>
        List<string> GetExampleCategoriesForInterest(string interest);
    }
}
