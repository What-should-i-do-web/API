using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Scores places based on explicit taste profile (from quiz and feedback).
    /// Uses interest dimension matching via PlaceCategoryMapper.
    /// </summary>
    public class ExplicitScorer : IExplicitScorer
    {
        private readonly IPlaceCategoryMapper _categoryMapper;

        public ExplicitScorer(IPlaceCategoryMapper categoryMapper)
        {
            _categoryMapper = categoryMapper;
        }

        public Task<double> ScoreAsync(
            UserTasteProfile? profile,
            Place place,
            CancellationToken cancellationToken = default)
        {
            // No profile → neutral score
            if (profile == null)
                return Task.FromResult(0.5);

            // No category → can't score
            if (string.IsNullOrWhiteSpace(place.Category))
                return Task.FromResult(0.5);

            // Map place category to interests
            var placeInterests = _categoryMapper.MapToInterests(place.Category);

            if (!placeInterests.Any())
                return Task.FromResult(0.5); // Unknown category

            // Get user's interest weights
            var userInterests = profile.GetInterestWeights();

            // Calculate weighted match score
            double totalScore = 0.0;
            double totalWeight = 0.0;

            foreach (var (interest, placeWeight) in placeInterests)
            {
                if (userInterests.TryGetValue(interest, out var userWeight))
                {
                    // Contribution = how much this place represents the interest × user's interest in it
                    totalScore += placeWeight * userWeight;
                    totalWeight += placeWeight;
                }
            }

            // Normalize by total place interest weights
            var finalScore = totalWeight > 0 ? totalScore / totalWeight : 0.5;

            // Ensure score is in [0,1] range
            finalScore = Math.Max(0.0, Math.Min(1.0, finalScore));

            return Task.FromResult(finalScore);
        }
    }
}
