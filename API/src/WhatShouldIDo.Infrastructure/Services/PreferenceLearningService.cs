using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;
using System.Text.Json;
using WhatShouldIDo.Application.Models;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class PreferenceLearningService : IPreferenceLearningService
    {
        private readonly WhatShouldIDoDbContext _context;
        private readonly ILogger<PreferenceLearningService> _logger;
        private readonly IAIService _aiService;

        public PreferenceLearningService(
            WhatShouldIDoDbContext context,
            ILogger<PreferenceLearningService> logger,
            IAIService aiService)
        {
            _context = context;
            _logger = logger;
            _aiService = aiService;
        }

        public async Task UpdateUserPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

                if (user == null) return;

                // Get user's visit history for learning
                var visits = await _context.UserVisits
                    .AsNoTracking()
                    .Where(v => v.UserId == userId && v.VisitConfirmed)
                    .OrderByDescending(v => v.VisitDate)
                    .Take(100) // Last 100 confirmed visits
                    .ToListAsync(cancellationToken);

                if (visits.Count < 5) return; // Need at least 5 visits for meaningful learning

                var preferences = await LearnFromVisitsAsync(userId, visits, cancellationToken);
                
                // Update user profile with learned preferences
                if (user.Profile == null)
                {
                    user.Profile = new UserProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId
                    };
                    _context.UserProfiles.Add(user.Profile);
                }

                // Update preferences as JSON
                user.Profile.FavoriteCuisines = JsonSerializer.Serialize(preferences.FavoriteCuisines);
                user.Profile.FavoriteActivityTypes = JsonSerializer.Serialize(preferences.FavoriteActivityTypes);
                user.Profile.AvoidedActivityTypes = JsonSerializer.Serialize(preferences.AvoidedActivityTypes);
                user.Profile.TimePreferences = JsonSerializer.Serialize(preferences.TimePreferences);
                user.Profile.PersonalizationScore = preferences.PersonalizationConfidence;
                user.Profile.LastPreferenceUpdate = DateTime.UtcNow;

                // Update main user preferences
                user.PreferredCuisines = JsonSerializer.Serialize(preferences.FavoriteCuisines.Take(5).ToList());
                user.ActivityPreferences = JsonSerializer.Serialize(preferences.FavoriteActivityTypes.Take(5).ToList());
                user.BudgetRange = preferences.PreferredBudgetRange;

                if (user.Profile.PreferredRadius == null || user.Profile.PreferredRadius < 1000)
                {
                    user.Profile.PreferredRadius = preferences.PreferredRadius;
                }

                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Updated preferences for user {UserId}: {Confidence}% confidence", 
                    userId, preferences.PersonalizationConfidence * 100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating preferences for user {UserId}", userId);
            }
        }

        public async Task<UserPreferences> GetLearnedPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

                if (user?.Profile == null)
                    return new UserPreferences();

                var preferences = new UserPreferences();

                // Parse JSON preferences
                if (!string.IsNullOrEmpty(user.Profile.FavoriteCuisines))
                    preferences.FavoriteCuisines = JsonSerializer.Deserialize<List<string>>(user.Profile.FavoriteCuisines) ?? new List<string>();

                if (!string.IsNullOrEmpty(user.Profile.FavoriteActivityTypes))
                    preferences.FavoriteActivityTypes = JsonSerializer.Deserialize<List<string>>(user.Profile.FavoriteActivityTypes) ?? new List<string>();

                if (!string.IsNullOrEmpty(user.Profile.AvoidedActivityTypes))
                    preferences.AvoidedActivityTypes = JsonSerializer.Deserialize<List<string>>(user.Profile.AvoidedActivityTypes) ?? new List<string>();

                if (!string.IsNullOrEmpty(user.Profile.TimePreferences))
                    preferences.TimePreferences = JsonSerializer.Deserialize<Dictionary<string, float>>(user.Profile.TimePreferences) ?? new Dictionary<string, float>();

                preferences.PreferredBudgetRange = user.BudgetRange ?? "medium";
                preferences.PreferredRadius = user.Profile.PreferredRadius ?? 3000;
                preferences.PersonalizationConfidence = user.Profile.PersonalizationScore;
                preferences.LastUpdated = user.Profile.LastPreferenceUpdate;

                return preferences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting preferences for user {UserId}", userId);
                return new UserPreferences();
            }
        }

        public async Task<float> CalculatePersonalizationScoreAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var visits = await _context.UserVisits
                    .AsNoTracking()
                    .Where(v => v.UserId == userId)
                    .ToListAsync(cancellationToken);

                if (visits.Count == 0) return 0f;

                var score = 0f;
                var maxScore = 0f;

                // Visit count factor (0-0.3)
                var visitScore = Math.Min(visits.Count / 50f, 0.3f);
                score += visitScore;
                maxScore += 0.3f;

                // Confirmed visits factor (0-0.2)
                var confirmedVisits = visits.Count(v => v.VisitConfirmed);
                var confirmationScore = Math.Min(confirmedVisits / 20f, 0.2f);
                score += confirmationScore;
                maxScore += 0.2f;

                // Rating feedback factor (0-0.2)
                var ratedVisits = visits.Count(v => v.UserRating.HasValue);
                var ratingScore = Math.Min(ratedVisits / 15f, 0.2f);
                score += ratingScore;
                maxScore += 0.2f;

                // Variety factor (0-0.15)
                var uniqueCategories = visits
                    .Where(v => !string.IsNullOrEmpty(v.PlaceName))
                    .Select(v => v.PlaceName)
                    .Distinct()
                    .Count();
                var varietyScore = Math.Min(uniqueCategories / 30f, 0.15f);
                score += varietyScore;
                maxScore += 0.15f;

                // Consistency factor (0-0.15)
                var recentVisits = visits.Where(v => v.VisitDate >= DateTime.UtcNow.AddDays(-30)).Count();
                var consistencyScore = Math.Min(recentVisits / 10f, 0.15f);
                score += consistencyScore;
                maxScore += 0.15f;

                return maxScore > 0 ? Math.Min(score / maxScore, 1.0f) : 0f;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating personalization score for user {UserId}", userId);
                return 0f;
            }
        }

        public async Task<List<string>> GetRecommendedCuisinesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var preferences = await GetLearnedPreferencesAsync(userId, cancellationToken);
            return preferences.FavoriteCuisines.Take(5).ToList();
        }

        public async Task<List<string>> GetRecommendedActivitiesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var preferences = await GetLearnedPreferencesAsync(userId, cancellationToken);
            return preferences.FavoriteActivityTypes.Take(5).ToList();
        }

        public async Task<string> GetOptimalTimePreferenceAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var preferences = await GetLearnedPreferencesAsync(userId, cancellationToken);
            
            if (preferences.TimePreferences.Count == 0)
                return "afternoon"; // Default

            return preferences.TimePreferences
                .OrderByDescending(kvp => kvp.Value)
                .First()
                .Key;
        }

        public async Task<Dictionary<string, float>> GetContextualPreferencesAsync(Guid userId, string timeOfDay, string dayOfWeek, CancellationToken cancellationToken = default)
        {
            try
            {
                var visits = await _context.UserVisits
                    .AsNoTracking()
                    .Where(v => v.UserId == userId && v.VisitConfirmed && v.UserRating >= 3.0f)
                    .ToListAsync(cancellationToken);

                var contextualPrefs = new Dictionary<string, float>();

                // Time-based preferences
                var timeVisits = visits.Where(v => v.TimeOfDay == timeOfDay).ToList();
                if (timeVisits.Any())
                {
                    var avgRating = timeVisits.Where(v => v.UserRating.HasValue).Average(v => v.UserRating.Value);
                    contextualPrefs[$"time_{timeOfDay}"] = avgRating / 5f; // Normalize to 0-1
                }

                // Day-based preferences
                var isWeekend = dayOfWeek == "saturday" || dayOfWeek == "sunday";
                var dayType = isWeekend ? "weekend" : "weekday";
                var dayVisits = visits.Where(v =>
                    (isWeekend && (v.DayOfWeek == "saturday" || v.DayOfWeek == "sunday")) ||
                    (!isWeekend && v.DayOfWeek != "saturday" && v.DayOfWeek != "sunday")
                ).ToList();

                if (dayVisits.Any())
                {
                    var avgRating = dayVisits.Where(v => v.UserRating.HasValue).Average(v => v.UserRating.Value);
                    contextualPrefs[$"day_{dayType}"] = avgRating / 5f;
                }

                return contextualPrefs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contextual preferences for user {UserId}", userId);
                return new Dictionary<string, float>();
            }
        }

        // ===== AI/ML Embedding Methods =====

        public async Task<float[]?> GetOrUpdateUserEmbeddingAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var profile = await _context.UserProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

                if (profile == null)
                {
                    _logger.LogWarning("User profile not found for user {UserId}", userId);
                    return null;
                }

                // Check if embedding exists and is recent (updated within 7 days)
                if (profile.PreferenceEmbedding != null &&
                    profile.PreferenceEmbedding.Length > 0 &&
                    profile.LastPreferenceUpdate >= DateTime.UtcNow.AddDays(-7))
                {
                    _logger.LogDebug("Using cached embedding for user {UserId}", userId);
                    return profile.PreferenceEmbedding;
                }

                // Check if user has enough data for meaningful embedding
                var actionCount = await _context.UserActions
                    .CountAsync(a => a.UserId == userId, cancellationToken);

                if (actionCount < 5)
                {
                    _logger.LogDebug("Insufficient data for user {UserId} (only {Count} actions)", userId, actionCount);
                    return null;
                }

                // Generate new embedding
                _logger.LogInformation("Generating new embedding for user {UserId}", userId);
                return await RegenerateUserEmbeddingAsync(userId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting/updating embedding for user {UserId}", userId);
                return null;
            }
        }

        public async Task<float[]> RegenerateUserEmbeddingAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get user preferences and actions
                var preferences = await GetLearnedPreferencesAsync(userId, cancellationToken);

                // Get recent user actions (last 100)
                var actions = await _context.UserActions
                    .AsNoTracking()
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.ActionTimestamp)
                    .Take(100)
                    .ToListAsync(cancellationToken);

                // Build a descriptive text summary of user preferences
                var preferenceText = BuildPreferenceText(preferences, actions);

                _logger.LogDebug("Generating embedding for user {UserId} with text: {Text}",
                    userId, preferenceText.Substring(0, Math.Min(100, preferenceText.Length)));

                // Generate embedding using AI service
                var embedding = await _aiService.GetEmbeddingAsync(preferenceText, cancellationToken);

                if (embedding == null || embedding.Length == 0)
                {
                    throw new InvalidOperationException("Failed to generate embedding");
                }

                // Save embedding to database
                var profile = await _context.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

                if (profile != null)
                {
                    profile.PreferenceEmbedding = embedding;
                    profile.LastPreferenceUpdate = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Successfully updated embedding for user {UserId} ({Dimensions} dimensions)",
                        userId, embedding.Length);
                }

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating embedding for user {UserId}", userId);
                throw;
            }
        }

        public async Task TrackUserActionAsync(
            Guid userId,
            string placeId,
            string actionType,
            string? placeName = null,
            string? category = null,
            float? rating = null,
            int? durationSeconds = null,
            string? metadata = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var action = new UserAction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PlaceId = placeId,
                    PlaceName = placeName,
                    Category = category,
                    ActionType = actionType,
                    Rating = rating,
                    DurationSeconds = durationSeconds,
                    Metadata = metadata,
                    ActionTimestamp = DateTime.UtcNow,
                    IsProcessed = false
                };

                _context.UserActions.Add(action);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Tracked {ActionType} action for user {UserId} on place {PlaceId}",
                    actionType, userId, placeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking user action for user {UserId}", userId);
            }
        }

        // ===== Private Helper Methods =====

        private string BuildPreferenceText(UserPreferences preferences, List<UserAction> actions)
        {
            var parts = new List<string>();

            // Add favorite cuisines
            if (preferences.FavoriteCuisines.Any())
            {
                parts.Add($"Favorite cuisines: {string.Join(", ", preferences.FavoriteCuisines)}");
            }

            // Add favorite activities
            if (preferences.FavoriteActivityTypes.Any())
            {
                parts.Add($"Favorite activities: {string.Join(", ", preferences.FavoriteActivityTypes)}");
            }

            // Add avoided types
            if (preferences.AvoidedActivityTypes.Any())
            {
                parts.Add($"Avoids: {string.Join(", ", preferences.AvoidedActivityTypes)}");
            }

            // Add budget preference
            parts.Add($"Budget preference: {preferences.PreferredBudgetRange}");

            // Add time preferences
            if (preferences.TimePreferences.Any())
            {
                var topTime = preferences.TimePreferences
                    .OrderByDescending(kvp => kvp.Value)
                    .First();
                parts.Add($"Prefers {topTime.Key} activities");
            }

            // Add recent highly-rated places
            var favoriteActions = actions
                .Where(a => a.Rating.HasValue && a.Rating.Value >= 4.0f && !string.IsNullOrEmpty(a.PlaceName))
                .Take(10)
                .Select(a => a.PlaceName!)
                .Distinct();

            if (favoriteActions.Any())
            {
                parts.Add($"Liked places: {string.Join(", ", favoriteActions)}");
            }

            // Add categories from actions
            var frequentCategories = actions
                .Where(a => !string.IsNullOrEmpty(a.Category))
                .GroupBy(a => a.Category!)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key);

            if (frequentCategories.Any())
            {
                parts.Add($"Frequently visits: {string.Join(", ", frequentCategories)}");
            }

            return string.Join(". ", parts) + ".";
        }

        private async Task<UserPreferences> LearnFromVisitsAsync(Guid userId, List<UserVisit> visits, CancellationToken cancellationToken)
        {
            var preferences = new UserPreferences();

            // Learn favorite cuisines from highly rated visits
            var cuisineFrequency = new Dictionary<string, (int count, float totalRating, float avgRating)>();
            var activityFrequency = new Dictionary<string, (int count, float totalRating, float avgRating)>();
            var timePreferences = new Dictionary<string, float>();
            var budgetAnalysis = new List<string>();

            foreach (var visit in visits.Where(v => v.UserRating.HasValue))
            {
                // Analyze place categories - get from Place table
                var place = await _context.Places
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == visit.PlaceId, cancellationToken);

                if (place?.Category != null)
                {
                    var categories = place.Category.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var category in categories)
                    {
                        var cleanCategory = category.Trim().ToLower();
                        var rating = visit.UserRating.Value;
                        
                        if (IsCuisineCategory(cleanCategory))
                        {
                            if (!cuisineFrequency.ContainsKey(cleanCategory))
                                cuisineFrequency[cleanCategory] = (0, 0f, 0f);
                            
                            var current = cuisineFrequency[cleanCategory];
                            cuisineFrequency[cleanCategory] = (
                                current.count + 1,
                                current.totalRating + rating,
                                (current.totalRating + rating) / (current.count + 1)
                            );
                        }
                        else
                        {
                            if (!activityFrequency.ContainsKey(cleanCategory))
                                activityFrequency[cleanCategory] = (0, 0f, 0f);
                            
                            var current = activityFrequency[cleanCategory];
                            activityFrequency[cleanCategory] = (
                                current.count + 1,
                                current.totalRating + rating,
                                (current.totalRating + rating) / (current.count + 1)
                            );
                        }
                    }
                }

                // Analyze time preferences
                if (!string.IsNullOrEmpty(visit.TimeOfDay))
                {
                    if (!timePreferences.ContainsKey(visit.TimeOfDay))
                        timePreferences[visit.TimeOfDay] = 0f;
                    
                    timePreferences[visit.TimeOfDay] += visit.UserRating.Value / 5f; // Normalize to 0-1
                }

                // Analyze price preferences
                if (!string.IsNullOrEmpty(place?.PriceLevel))
                {
                    budgetAnalysis.Add(place.PriceLevel);
                }
            }

            // Set favorite cuisines (rating >= 3.5 and count >= 2)
            preferences.FavoriteCuisines = cuisineFrequency
                .Where(kvp => kvp.Value.avgRating >= 3.5f && kvp.Value.count >= 2)
                .OrderByDescending(kvp => kvp.Value.avgRating)
                .ThenByDescending(kvp => kvp.Value.count)
                .Take(8)
                .Select(kvp => kvp.Key)
                .ToList();

            // Set favorite activities
            preferences.FavoriteActivityTypes = activityFrequency
                .Where(kvp => kvp.Value.avgRating >= 3.5f && kvp.Value.count >= 2)
                .OrderByDescending(kvp => kvp.Value.avgRating)
                .ThenByDescending(kvp => kvp.Value.count)
                .Take(8)
                .Select(kvp => kvp.Key)
                .ToList();

            // Set avoided categories (rating < 2.5)
            preferences.AvoidedActivityTypes = activityFrequency
                .Where(kvp => kvp.Value.avgRating < 2.5f)
                .Select(kvp => kvp.Key)
                .ToList();

            // Normalize time preferences
            if (timePreferences.Any())
            {
                var totalVisits = visits.Count;
                preferences.TimePreferences = timePreferences
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value / totalVisits);
            }

            // Determine preferred budget range
            if (budgetAnalysis.Any())
            {
                var mostCommonBudget = budgetAnalysis
                    .GroupBy(b => b)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;
                
                preferences.PreferredBudgetRange = mostCommonBudget.ToLower() switch
                {
                    "inexpensive" or "1" => "low",
                    "moderate" or "2" => "medium", 
                    "expensive" or "3" => "high",
                    "very expensive" or "4" => "high",
                    _ => "medium"
                };
            }

            preferences.PersonalizationConfidence = await CalculatePersonalizationScoreAsync(userId);

            return preferences;
        }

        private static bool IsCuisineCategory(string category)
        {
            var cuisineCategories = new[]
            {
                "restaurant", "food", "meal_takeaway", "bakery", "cafe",
                "turkish", "italian", "chinese", "indian", "mexican", "french",
                "japanese", "korean", "thai", "mediterranean", "american",
                "fast_food", "pizza", "seafood", "steakhouse", "vegetarian"
            };

            return cuisineCategories.Any(c => category.Contains(c));
        }
    }
}