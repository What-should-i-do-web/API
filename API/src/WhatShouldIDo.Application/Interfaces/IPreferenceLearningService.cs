using WhatShouldIDo.Domain.Entities;

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
    }

    public class UserPreferences
    {
        public List<string> FavoriteCuisines { get; set; } = new();
        public List<string> FavoriteActivityTypes { get; set; } = new();
        public List<string> AvoidedCuisines { get; set; } = new();
        public List<string> AvoidedActivityTypes { get; set; } = new();
        public Dictionary<string, float> TimePreferences { get; set; } = new(); // "morning": 0.8
        public Dictionary<string, float> DayPreferences { get; set; } = new(); // "weekend": 0.9
        public string PreferredBudgetRange { get; set; } = "medium";
        public int PreferredRadius { get; set; } = 3000;
        public float PersonalizationConfidence { get; set; } = 0.0f;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}