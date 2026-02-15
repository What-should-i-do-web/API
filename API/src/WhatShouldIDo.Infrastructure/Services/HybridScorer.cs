using Microsoft.Extensions.Options;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.Models;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Hybrid scorer that combines multiple scoring dimensions:
    /// - Implicit (learned from history)
    /// - Explicit (taste profile)
    /// - Novelty (trying new things)
    /// - Context (time, weather, location)
    /// - Quality (rating, reviews, distance)
    /// </summary>
    public class HybridScorer : IHybridScorer
    {
        private readonly IImplicitScorer _implicitScorer;
        private readonly IExplicitScorer _explicitScorer;
        private readonly IVariabilityEngine _variabilityEngine;
        private readonly IContextEngine _contextEngine;
        private readonly IExplainabilityService _explainabilityService;
        private readonly ITasteProfileRepository _tasteProfileRepository;
        private readonly IPreferenceLearningService _preferenceLearningService;
        private readonly RecommendationScoringOptions _options;

        public HybridScorer(
            IImplicitScorer implicitScorer,
            IExplicitScorer explicitScorer,
            IVariabilityEngine variabilityEngine,
            IContextEngine contextEngine,
            IExplainabilityService explainabilityService,
            ITasteProfileRepository tasteProfileRepository,
            IPreferenceLearningService preferenceLearningService,
            IOptions<RecommendationScoringOptions> options)
        {
            _implicitScorer = implicitScorer;
            _explicitScorer = explicitScorer;
            _variabilityEngine = variabilityEngine;
            _contextEngine = contextEngine;
            _explainabilityService = explainabilityService;
            _tasteProfileRepository = tasteProfileRepository;
            _preferenceLearningService = preferenceLearningService;
            _options = options.Value;
        }

        public async Task<List<ScoredPlace>> ScoreAndExplainAsync(
            Guid userId,
            List<Place> candidates,
            ScoringContext context,
            CancellationToken cancellationToken = default)
        {
            // Load user data if not provided in context
            if (context.TasteProfile == null)
            {
                context.TasteProfile = await _tasteProfileRepository.GetByUserIdAsync(userId, cancellationToken);
            }

            if (context.ImplicitPreferences == null)
            {
                context.ImplicitPreferences = await _preferenceLearningService.GetLearnedPreferencesAsync(userId, cancellationToken);
            }

            // Score each place
            var scoredPlaces = new List<ScoredPlace>();

            foreach (var place in candidates)
            {
                var scoredPlace = await ScorePlaceAsync(userId, place, context, cancellationToken);
                scoredPlaces.Add(scoredPlace);
            }

            // Sort by score (descending)
            return scoredPlaces.OrderByDescending(sp => sp.Score).ToList();
        }

        public async Task<ScoredPlace> ScorePlaceAsync(
            Guid userId,
            Place place,
            ScoringContext context,
            CancellationToken cancellationToken = default)
        {
            // Calculate individual score components
            var implicitScore = await _implicitScorer.ScoreAsync(userId, place, context.ImplicitPreferences, cancellationToken);
            var explicitScore = await _explicitScorer.ScoreAsync(context.TasteProfile, place, cancellationToken);
            var noveltyScore = await _variabilityEngine.CalculateNoveltyScoreAsync(userId, place, cancellationToken);
            var contextScore = await CalculateContextScoreAsync(place, context);
            var qualityScore = CalculateQualityScore(place, context);

            // Build score breakdown
            var breakdown = new ScoreBreakdown
            {
                ImplicitScore = implicitScore,
                ImplicitWeight = _options.ImplicitWeight,
                ExplicitScore = explicitScore,
                ExplicitWeight = _options.ExplicitWeight,
                NoveltyScore = noveltyScore,
                NoveltyWeight = _options.NoveltyWeight,
                ContextScore = contextScore,
                ContextWeight = _options.ContextWeight,
                QualityScore = qualityScore,
                QualityWeight = _options.QualityWeight
            };

            // Calculate final score (weighted sum)
            var finalScore = breakdown.FinalScore;

            // Generate explanations
            var reasons = _explainabilityService.GenerateReasons(place, breakdown, context);

            return new ScoredPlace
            {
                Place = place,
                Score = finalScore,
                Reasons = reasons,
                Debug = _options.EnableDebugFields ? breakdown : null
            };
        }

        /// <summary>
        /// Calculate context score based on time, weather, location appropriateness.
        /// Uses existing ContextEngine.
        /// </summary>
        private async Task<double> CalculateContextScoreAsync(Place place, ScoringContext context)
        {
            try
            {
                // Use existing context engine if available
                var contextualInsights = await _contextEngine.GetContextualInsights(
                    place.Latitude,
                    place.Longitude,
                    CancellationToken.None);

                var contextualReasons = await _contextEngine.GetContextualReasons(place, contextualInsights);

                // Score based on number of contextual matches
                // More reasons = better contextual fit
                var score = contextualReasons.Count > 0 ? 0.5 + (contextualReasons.Count * 0.1) : 0.5;
                return Math.Min(1.0, score);
            }
            catch
            {
                // Fallback to neutral if context engine fails
                return 0.5;
            }
        }

        /// <summary>
        /// Calculate quality score based on rating, review count, and distance.
        /// </summary>
        private double CalculateQualityScore(Place place, ScoringContext context)
        {
            double score = 0.5; // Base

            // Rating component (40% of quality score)
            if (double.TryParse(place.Rating, out var rating) && rating > 0)
            {
                var normalizedRating = rating / 5.0;

                // Apply review count smoothing to avoid high-rating-few-reviews bias
                var reviewCount = place.ReviewCount ?? 0;
                var smoothing = _options.ReviewCountSmoothingFactor;
                var confidence = reviewCount / (reviewCount + smoothing);

                var ratingScore = normalizedRating * confidence;
                score += ratingScore * 0.4;
            }

            // Distance component (60% of quality score)
            if (context.Origin.HasValue && place.Latitude != 0 && place.Longitude != 0)
            {
                var distance = CalculateDistance(
                    context.Origin.Value.Latitude,
                    context.Origin.Value.Longitude,
                    place.Latitude,
                    place.Longitude);

                var distanceScore = CalculateDistanceScore(distance);
                score += distanceScore * 0.6;
            }

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        /// <summary>
        /// Calculate distance score with linear penalty.
        /// </summary>
        private double CalculateDistanceScore(double distanceMeters)
        {
            var startPenalty = _options.DistancePenaltyStartMeters;
            var maxPenalty = _options.DistancePenaltyMaxMeters;

            if (distanceMeters <= startPenalty)
                return 1.0; // Full score for close places

            if (distanceMeters >= maxPenalty)
                return 0.0; // Zero score for very far places

            // Linear decay between start and max
            var range = maxPenalty - startPenalty;
            var excess = distanceMeters - startPenalty;
            return 1.0 - (excess / range);
        }

        /// <summary>
        /// Calculate distance between two coordinates using Haversine formula.
        /// Returns distance in meters.
        /// </summary>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusKm = 6371;

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distanceKm = EarthRadiusKm * c;

            return distanceKm * 1000; // Convert to meters
        }

        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
