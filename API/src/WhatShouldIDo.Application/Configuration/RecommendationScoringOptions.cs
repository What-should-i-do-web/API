using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.Configuration
{
    /// <summary>
    /// Configuration for the hybrid recommendation scoring system.
    /// Weights control how implicit learning, explicit taste profile, novelty, context, and quality
    /// are balanced in the final recommendation score.
    /// </summary>
    public class RecommendationScoringOptions
    {
        // ========================
        // Scoring Weights
        // ========================
        // All weights should sum to 1.0 for normalized scoring

        /// <summary>
        /// Weight for implicit learning score (from visit history and preferences).
        /// Default: 0.25 (25%)
        /// </summary>
        [Range(0.0, 1.0)]
        public double ImplicitWeight { get; set; } = 0.25;

        /// <summary>
        /// Weight for explicit taste profile score (from quiz and feedback).
        /// Default: 0.30 (30%)
        /// </summary>
        [Range(0.0, 1.0)]
        public double ExplicitWeight { get; set; } = 0.30;

        /// <summary>
        /// Weight for novelty score (trying new experiences).
        /// Default: 0.20 (20%)
        /// </summary>
        [Range(0.0, 1.0)]
        public double NoveltyWeight { get; set; } = 0.20;

        /// <summary>
        /// Weight for context score (time, weather, location appropriateness).
        /// Default: 0.15 (15%)
        /// </summary>
        [Range(0.0, 1.0)]
        public double ContextWeight { get; set; } = 0.15;

        /// <summary>
        /// Weight for quality score (rating, review count, distance).
        /// Default: 0.10 (10%)
        /// </summary>
        [Range(0.0, 1.0)]
        public double QualityWeight { get; set; } = 0.10;

        // ========================
        // Caching Configuration
        // ========================

        /// <summary>
        /// How long to cache candidate place sets (minutes).
        /// Candidate caching reduces external API calls significantly.
        /// Default: 15 minutes
        /// </summary>
        [Range(1, 60)]
        public int CandidateCacheTtlMinutes { get; set; } = 15;

        /// <summary>
        /// How long to cache place details (minutes).
        /// Place details change infrequently so can be cached longer.
        /// Default: 60 minutes
        /// </summary>
        [Range(1, 1440)]
        public int PlaceDetailsCacheTtlMinutes { get; set; } = 60;

        // ========================
        // Result Limits
        // ========================

        /// <summary>
        /// Maximum number of candidates to fetch from providers before filtering.
        /// Higher = more choices but more cost. Lower = faster but less variety.
        /// Default: 100
        /// </summary>
        [Range(10, 500)]
        public int MaxCandidates { get; set; } = 100;

        /// <summary>
        /// Maximum number of results to return after scoring and ranking.
        /// Default: 20
        /// </summary>
        [Range(1, 50)]
        public int MaxResults { get; set; } = 20;

        /// <summary>
        /// Default search radius in meters when not specified.
        /// Default: 3000 (3km)
        /// </summary>
        [Range(100, 50000)]
        public int DefaultRadius { get; set; } = 3000;

        // ========================
        // Quality Scoring Parameters
        // ========================

        /// <summary>
        /// Smoothing factor for review count when calculating quality score.
        /// Prevents places with few reviews but high rating from dominating.
        /// Formula: score = rating * (reviewCount / (reviewCount + smoothing))
        /// Default: 50
        /// </summary>
        [Range(1, 200)]
        public int ReviewCountSmoothingFactor { get; set; } = 50;

        /// <summary>
        /// Minimum rating threshold (0-5) for a place to be recommended.
        /// Places below this rating are filtered out.
        /// Default: 0 (no filtering, let scoring handle it)
        /// </summary>
        [Range(0.0, 5.0)]
        public double MinimumRating { get; set; } = 0.0;

        // ========================
        // Distance Scoring Parameters
        // ========================

        /// <summary>
        /// Distance penalty starts at this threshold (meters).
        /// Places closer than this get full distance score (1.0).
        /// Default: 500 (0.5km)
        /// </summary>
        [Range(0, 5000)]
        public int DistancePenaltyStartMeters { get; set; } = 500;

        /// <summary>
        /// Distance at which score reaches zero (meters).
        /// Places farther than this get zero distance score.
        /// Default: 5000 (5km)
        /// </summary>
        [Range(1000, 50000)]
        public int DistancePenaltyMaxMeters { get; set; } = 5000;

        // ========================
        // Debug Configuration
        // ========================

        /// <summary>
        /// Enable debug fields in recommendation responses (score breakdown).
        /// Should be false in production to avoid exposing internal scoring logic.
        /// Default: false
        /// </summary>
        public bool EnableDebugFields { get; set; } = false;

        /// <summary>
        /// Validate that weights sum to approximately 1.0 (within tolerance).
        /// </summary>
        public bool ValidateWeights()
        {
            var sum = ImplicitWeight + ExplicitWeight + NoveltyWeight + ContextWeight + QualityWeight;
            const double tolerance = 0.01; // Allow 1% variance
            return Math.Abs(sum - 1.0) <= tolerance;
        }
    }
}
