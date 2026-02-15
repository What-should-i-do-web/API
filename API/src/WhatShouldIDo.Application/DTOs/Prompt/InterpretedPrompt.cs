using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Prompt
{
    /// <summary>
    /// AI-interpreted structured data from natural language prompt
    /// </summary>
    public class InterpretedPrompt
    {
        /// <summary>
        /// Original user prompt text
        /// </summary>
        public string OriginalPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Extracted search query text (e.g., "cheap vegan food")
        /// </summary>
        public string TextQuery { get; set; } = string.Empty;

        /// <summary>
        /// Extracted location text (e.g., "Kadıköy"), null if not specified
        /// </summary>
        public string? LocationText { get; set; }

        /// <summary>
        /// Price level preferences (e.g., ["PRICE_LEVEL_INEXPENSIVE", "PRICE_LEVEL_MODERATE"])
        /// </summary>
        public string[] PricePreferences { get; set; } = [];

        /// <summary>
        /// Extracted place categories/types (e.g., ["restaurant", "cafe", "bar"])
        /// </summary>
        public List<string> Categories { get; set; } = new();

        /// <summary>
        /// Dietary preferences (e.g., ["vegan", "vegetarian", "gluten-free"])
        /// </summary>
        public List<string> DietaryRestrictions { get; set; } = new();

        /// <summary>
        /// Time-related preferences (e.g., "breakfast", "lunch", "dinner", "evening")
        /// </summary>
        public string? TimeContext { get; set; }

        /// <summary>
        /// Atmosphere or mood (e.g., "romantic", "casual", "quiet", "lively")
        /// </summary>
        public List<string> Atmosphere { get; set; } = new();

        /// <summary>
        /// Activity types (e.g., ["dining", "sightseeing", "shopping", "entertainment"])
        /// </summary>
        public List<string> ActivityTypes { get; set; } = new();

        /// <summary>
        /// Extracted tags or keywords
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Suggested search radius in meters (AI-inferred based on context)
        /// </summary>
        public int? SuggestedRadius { get; set; }

        /// <summary>
        /// Confidence score of interpretation (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; } = 1.0;

        /// <summary>
        /// Additional context or notes from AI interpretation
        /// </summary>
        public string? AIReasoning { get; set; }
    }
}
