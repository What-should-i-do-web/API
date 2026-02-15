using System.Collections.Generic;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request to manually update taste profile weights.
    /// </summary>
    public class UpdateTasteProfileRequest
    {
        /// <summary>
        /// Dictionary of weight keys to new values (0.0-1.0).
        /// Valid keys: CultureWeight, FoodWeight, NatureWeight, NightlifeWeight,
        /// ShoppingWeight, ArtWeight, WellnessWeight, SportsWeight,
        /// TasteQualityWeight, AtmosphereWeight, DesignWeight,
        /// CalmnessWeight, SpaciousnessWeight, NoveltyTolerance.
        /// </summary>
        public Dictionary<string, double> Weights { get; set; } = new();
    }
}
