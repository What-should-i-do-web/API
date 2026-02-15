using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Application.Models;
namespace WhatShouldIDo.Application.Interfaces
{
    public interface IPreferenceLearningService
    {
        Task UpdateUserPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserPreferences> GetLearnedPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<float> CalculatePersonalizationScoreAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<List<string>> GetRecommendedCuisinesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<List<string>> GetRecommendedActivitiesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<string> GetOptimalTimePreferenceAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<Dictionary<string, float>> GetContextualPreferencesAsync(Guid userId, string timeOfDay, string dayOfWeek, CancellationToken cancellationToken = default);

        // ===== AI/ML Embedding Methods =====

        /// <summary>
        /// Gets cached user embedding or generates a new one if not available or stale.
        /// Returns null if user has insufficient data for embedding generation.
        /// </summary>
        Task<float[]?> GetOrUpdateUserEmbeddingAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Forces regeneration of user preference embedding based on current actions and preferences.
        /// Uses AI to generate a semantic embedding vector for personalization.
        /// </summary>
        Task<float[]> RegenerateUserEmbeddingAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tracks a user action (view, favorite, visit, rate, etc.) for learning purposes.
        /// These actions are later processed to update user embeddings.
        /// </summary>
        Task TrackUserActionAsync(
            Guid userId,
            string placeId,
            string actionType,
            string? placeName = null,
            string? category = null,
            float? rating = null,
            int? durationSeconds = null,
            string? metadata = null,
            CancellationToken cancellationToken = default);
    }

   
}