using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.Models;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Scores places based on implicit learning from user's visit history.
    /// This is the existing scoring logic extracted from SmartSuggestionService.
    /// </summary>
    public class ImplicitScorer : IImplicitScorer
    {
        private readonly IVariabilityEngine _variabilityEngine;
        private readonly IVisitTrackingService _visitTrackingService;

        public ImplicitScorer(
            IVariabilityEngine variabilityEngine,
            IVisitTrackingService visitTrackingService)
        {
            _variabilityEngine = variabilityEngine;
            _visitTrackingService = visitTrackingService;
        }

        public async Task<double> ScoreAsync(
            Guid userId,
            Place place,
            UserPreferences? preferences,
            CancellationToken cancellationToken = default)
        {
            // Start with base score
            var score = 0.5;

            // No preferences → return neutral
            if (preferences == null)
                return score;

            // Category preference boost
            if (place.Category != null && preferences.FavoriteCuisines.Any())
            {
                var placeCategories = place.Category.ToLower();
                foreach (var favCuisine in preferences.FavoriteCuisines)
                {
                    if (placeCategories.Contains(favCuisine.ToLower()))
                    {
                        score += 0.3;
                        break;
                    }
                }
            }

            // Activity type boost
            if (place.Category != null && preferences.FavoriteActivityTypes.Any())
            {
                var placeCategories = place.Category.ToLower();
                foreach (var favActivity in preferences.FavoriteActivityTypes)
                {
                    if (placeCategories.Contains(favActivity.ToLower()))
                    {
                        score += 0.2;
                        break;
                    }
                }
            }

            // Avoidance penalty
            if (place.Category != null && preferences.AvoidedActivityTypes.Any())
            {
                var placeCategories = place.Category.ToLower();
                foreach (var avoidedType in preferences.AvoidedActivityTypes)
                {
                    if (placeCategories.Contains(avoidedType.ToLower()))
                    {
                        score -= 0.4;
                        break;
                    }
                }
            }

            // Novelty boost (from VariabilityEngine)
            var noveltyScore = await _variabilityEngine.CalculateNoveltyScoreAsync(userId, place, cancellationToken);
            score += noveltyScore * 0.2;

            // Avoidance score penalty (recently visited, poorly rated)
            var avoidanceScore = await _visitTrackingService.GetPlaceAvoidanceScoreAsync(userId, place, cancellationToken);
            score -= avoidanceScore * 0.3;

            // Original rating factor
            if (place.Rating != null && double.TryParse(place.Rating, out var rating))
            {
                score += (rating / 5.0) * 0.1; // 10% weight for original rating
            }

            // Clamp to [0,1] range
            return Math.Max(0.0, Math.Min(1.0, score));
        }
    }
}
