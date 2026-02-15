using System.Collections.Generic;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Models
{
    /// <summary>
    /// Context information for recommendation scoring.
    /// Includes user preferences, location, time, and other factors.
    /// </summary>
    public class ScoringContext
    {
        /// <summary>
        /// User ID for personalization.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// User's learned preferences (implicit learning).
        /// Null if user has no history.
        /// </summary>
        public UserPreferences? ImplicitPreferences { get; set; }

        /// <summary>
        /// User's taste profile (explicit preferences).
        /// Null if user hasn't taken quiz.
        /// </summary>
        public UserTasteProfile? TasteProfile { get; set; }

        /// <summary>
        /// Origin location for distance scoring.
        /// </summary>
        public (double Latitude, double Longitude)? Origin { get; set; }

        /// <summary>
        /// Current time for context scoring.
        /// </summary>
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Original user prompt/intent (optional).
        /// </summary>
        public string? Prompt { get; set; }

        /// <summary>
        /// Session identifier for grouping related requests.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Whether to include debug score breakdown in results.
        /// </summary>
        public bool IncludeDebugInfo { get; set; }
    }

    /// <summary>
    /// A place with its computed score and explanation.
    /// </summary>
    public class ScoredPlace
    {
        /// <summary>
        /// The place being scored.
        /// </summary>
        public Place Place { get; set; } = null!;

        /// <summary>
        /// Final combined score (0-1, higher is better).
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Human-readable reasons why this place was recommended.
        /// </summary>
        public List<RecommendationReasonDto> Reasons { get; set; } = new();

        /// <summary>
        /// Detailed score breakdown (only when debug enabled).
        /// </summary>
        public ScoreBreakdown? Debug { get; set; }
    }

    /// <summary>
    /// Detailed breakdown of how the final score was calculated.
    /// Only included when EnableDebugFields is true.
    /// </summary>
    public class ScoreBreakdown
    {
        public double ImplicitScore { get; set; }
        public double ImplicitWeight { get; set; }
        public double ImplicitContribution => ImplicitScore * ImplicitWeight;

        public double ExplicitScore { get; set; }
        public double ExplicitWeight { get; set; }
        public double ExplicitContribution => ExplicitScore * ExplicitWeight;

        public double NoveltyScore { get; set; }
        public double NoveltyWeight { get; set; }
        public double NoveltyContribution => NoveltyScore * NoveltyWeight;

        public double ContextScore { get; set; }
        public double ContextWeight { get; set; }
        public double ContextContribution => ContextScore * ContextWeight;

        public double QualityScore { get; set; }
        public double QualityWeight { get; set; }
        public double QualityContribution => QualityScore * QualityWeight;

        public double FinalScore => ImplicitContribution + ExplicitContribution +
                                    NoveltyContribution + ContextContribution + QualityContribution;
    }

    /// <summary>
    /// User preferences from implicit learning (existing model).
    /// Defined here for reference - actual implementation is in PreferenceLearningService.
    /// </summary>
    public class UserPreferences
    {
        public List<string> FavoriteCuisines { get; set; } = new();
        public List<string> FavoriteActivityTypes { get; set; } = new();
        public List<string> FavoriteCategories { get; set; } = new();

        public List<string> AvoidedCuisines { get; set; } = new();
        public List<string> AvoidedActivityTypes { get; set; } = new();

        public List<string> DietaryRestrictions { get; set; } = new();

        public Dictionary<string, float> TimePreferences { get; set; } = new();
        public Dictionary<string, float> DayPreferences { get; set; } = new();

        public string PreferredBudgetRange { get; set; } = "medium";
        public int PreferredRadius { get; set; } = 3000;

        public float PersonalizationConfidence { get; set; } = 0.0f;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
