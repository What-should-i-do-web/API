using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class VariabilityEngine : IVariabilityEngine
    {
        private readonly WhatShouldIDoDbContext _context;
        private readonly IVisitTrackingService _visitTrackingService;
        private readonly ILogger<VariabilityEngine> _logger;

        public VariabilityEngine(
            WhatShouldIDoDbContext context, 
            IVisitTrackingService visitTrackingService,
            ILogger<VariabilityEngine> logger)
        {
            _context = context;
            _visitTrackingService = visitTrackingService;
            _logger = logger;
        }

        public async Task<List<Place>> FilterForVarietyAsync(Guid userId, List<Place> places, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!places.Any()) return places;

                // Get recently visited places (last 30 days)
                var recentlyVisited = await _visitTrackingService.GetRecentlyVisitedPlacesAsync(userId, 30, cancellationToken);
                var visitedPlaceIds = recentlyVisited.Select(p => p.Id).ToHashSet();

                // Filter out recently visited places
                var filteredPlaces = places.Where(p => !visitedPlaceIds.Contains(p.Id)).ToList();

                // If we filtered out too many, add back some older visits (prefer 7+ days old)
                if (filteredPlaces.Count < places.Count * 0.7) // Keep at least 70% of original
                {
                    var olderVisited = await _visitTrackingService.GetRecentlyVisitedPlacesAsync(userId, 7, cancellationToken);
                    var recentPlaceIds = olderVisited.Select(p => p.Id).ToHashSet();
                    
                    var allowedOlder = places.Where(p => visitedPlaceIds.Contains(p.Id) && !recentPlaceIds.Contains(p.Id));
                    filteredPlaces.AddRange(allowedOlder);
                }

                // Apply category variety - avoid too many of same type
                filteredPlaces = await ApplyCategoryVarietyAsync(filteredPlaces, cancellationToken);

                _logger.LogDebug("Filtered places for variety: {Original} → {Filtered} places for user {UserId}", 
                    places.Count, filteredPlaces.Count, userId);

                return filteredPlaces;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering places for variety for user {UserId}", userId);
                return places; // Return original list on error
            }
        }

        public async Task<List<Place>> ApplyDiscoveryBoostAsync(Guid userId, List<Place> places, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!places.Any()) return places;

                var boostedPlaces = new List<Place>();
                var regularPlaces = new List<Place>();

                foreach (var place in places)
                {
                    var noveltyScore = await CalculateNoveltyScoreAsync(userId, place, cancellationToken);
                    
                    // Apply discovery boost for novel places
                    if (noveltyScore > 0.7f)
                    {
                        boostedPlaces.Add(place);
                    }
                    else
                    {
                        regularPlaces.Add(place);
                    }
                }

                // Mix discovery places with regular places (30% discovery, 70% regular)
                var result = new List<Place>();
                var discoveryCount = Math.Min(boostedPlaces.Count, (int)(places.Count * 0.3));
                
                result.AddRange(boostedPlaces.Take(discoveryCount));
                result.AddRange(regularPlaces.Take(places.Count - discoveryCount));

                _logger.LogDebug("Applied discovery boost: {Discovery} discovery places for user {UserId}", 
                    discoveryCount, userId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying discovery boost for user {UserId}", userId);
                return places;
            }
        }

        public async Task<List<Place>> ApplySeasonalVarietyAsync(List<Place> places, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!places.Any()) return places;

                var currentMonth = DateTime.UtcNow.Month;
                var season = GetSeason(currentMonth);
                
                // Boost seasonal places
                var seasonalPlaces = new List<Place>();
                var nonSeasonalPlaces = new List<Place>();

                foreach (var place in places)
                {
                    if (IsSeasonallyRelevant(place, season))
                    {
                        seasonalPlaces.Add(place);
                    }
                    else
                    {
                        nonSeasonalPlaces.Add(place);
                    }
                }

                // Prioritize seasonal places (40% seasonal, 60% regular)
                var result = new List<Place>();
                var seasonalCount = Math.Min(seasonalPlaces.Count, (int)(places.Count * 0.4));
                
                result.AddRange(seasonalPlaces.Take(seasonalCount));
                result.AddRange(nonSeasonalPlaces.Take(places.Count - seasonalCount));

                _logger.LogDebug("Applied seasonal variety: {Seasonal} seasonal places for {Season}", 
                    seasonalCount, season);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying seasonal variety");
                return places;
            }
        }

        public async Task<List<Place>> ApplyContextualVarietyAsync(Guid userId, List<Place> places, string timeOfDay, string dayOfWeek, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!places.Any()) return places;

                // Get user's contextual preferences
                var contextualPrefs = await GetContextualPreferencesAsync(userId, timeOfDay, dayOfWeek, cancellationToken);
                
                // Score places based on contextual appropriateness
                var scoredPlaces = places.Select(place => new
                {
                    Place = place,
                    ContextScore = CalculateContextualScore(place, timeOfDay, dayOfWeek, contextualPrefs)
                }).ToList();

                // Mix contextually appropriate with variety
                var contextualPlaces = scoredPlaces
                    .Where(sp => sp.ContextScore > 0.6f)
                    .OrderByDescending(sp => sp.ContextScore)
                    .Select(sp => sp.Place)
                    .ToList();

                var otherPlaces = scoredPlaces
                    .Where(sp => sp.ContextScore <= 0.6f)
                    .Select(sp => sp.Place)
                    .ToList();

                // 50% contextually appropriate, 50% variety
                var result = new List<Place>();
                var contextualCount = Math.Min(contextualPlaces.Count, places.Count / 2);
                
                result.AddRange(contextualPlaces.Take(contextualCount));
                result.AddRange(otherPlaces.Take(places.Count - contextualCount));

                _logger.LogDebug("Applied contextual variety: {Contextual} contextual places for {Time}/{Day}", 
                    contextualCount, timeOfDay, dayOfWeek);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying contextual variety for user {UserId}", userId);
                return places;
            }
        }

        public async Task<float> CalculateNoveltyScoreAsync(Guid userId, Place place, CancellationToken cancellationToken = default)
        {
            try
            {
                var noveltyScore = 1.0f; // Start with maximum novelty

                // Check if user has visited this place
                var hasVisited = await _visitTrackingService.HasUserVisitedPlaceAsync(userId, place.Id, 90, cancellationToken);
                if (hasVisited)
                {
                    noveltyScore -= 0.8f; // Heavy penalty for visited places
                }

                // Check category familiarity
                var userPreferences = await GetUserCategoryExperienceAsync(userId, cancellationToken);
                if (place.Category != null)
                {
                    var categories = place.Category.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var familiarityPenalty = 0f;

                    foreach (var category in categories)
                    {
                        var cleanCategory = category.Trim().ToLower();
                        if (userPreferences.ContainsKey(cleanCategory))
                        {
                            // More familiar = lower novelty
                            familiarityPenalty += Math.Min(userPreferences[cleanCategory] / 10f, 0.3f);
                        }
                    }

                    noveltyScore -= Math.Min(familiarityPenalty, 0.4f);
                }

                // Check location novelty (distance from usual places)
                var locationNovelty = await CalculateLocationNoveltyAsync(userId, place, cancellationToken);
                noveltyScore += locationNovelty * 0.2f; // Boost for new areas

                return Math.Max(0f, Math.Min(noveltyScore, 1.0f));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating novelty score for user {UserId}", userId);
                return 0.5f; // Neutral novelty on error
            }
        }

        public async Task<List<Place>> RankByVarietyAsync(Guid userId, List<Place> places, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!places.Any()) return places;

                var scoredPlaces = new List<(Place place, float varietyScore)>();

                foreach (var place in places)
                {
                    var varietyScore = 0f;

                    // Novelty factor (40%)
                    var noveltyScore = await CalculateNoveltyScoreAsync(userId, place, cancellationToken);
                    varietyScore += noveltyScore * 0.4f;

                    // Category diversity factor (30%)
                    var diversityScore = CalculateCategoryDiversityScore(place, places);
                    varietyScore += diversityScore * 0.3f;

                    // Distance variety factor (20%)
                    var distanceScore = await CalculateDistanceVarietyAsync(userId, place, cancellationToken);
                    varietyScore += distanceScore * 0.2f;

                    // Time appropriateness factor (10%)
                    var timeScore = CalculateTimeAppropriatenessScore(place, DateTime.Now.Hour);
                    varietyScore += timeScore * 0.1f;

                    scoredPlaces.Add((place, varietyScore));
                }

                // Sort by variety score, but maintain some randomness
                var random = new Random();
                return scoredPlaces
                    .OrderByDescending(sp => sp.varietyScore + (random.NextDouble() * 0.1)) // Add small random factor
                    .Select(sp => sp.place)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ranking places by variety for user {UserId}", userId);
                return places.OrderBy(p => Guid.NewGuid()).ToList(); // Random order on error
            }
        }

        // Private helper methods
        private async Task<List<Place>> ApplyCategoryVarietyAsync(List<Place> places, CancellationToken cancellationToken)
        {
            var categoryCount = new Dictionary<string, int>();
            var result = new List<Place>();

            foreach (var place in places)
            {
                if (place.Category == null)
                {
                    result.Add(place);
                    continue;
                }

                var categories = place.Category.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var mainCategory = categories.FirstOrDefault()?.Trim().ToLower();

                if (mainCategory == null)
                {
                    result.Add(place);
                    continue;
                }

                // Limit same category to max 3 places
                if (categoryCount.GetValueOrDefault(mainCategory, 0) < 3)
                {
                    result.Add(place);
                    categoryCount[mainCategory] = categoryCount.GetValueOrDefault(mainCategory, 0) + 1;
                }
            }

            return result;
        }

        private async Task<Dictionary<string, float>> GetContextualPreferencesAsync(Guid userId, string timeOfDay, string dayOfWeek, CancellationToken cancellationToken)
        {
            // This would ideally use the PreferenceLearningService
            // For now, return basic preferences
            return new Dictionary<string, float>
            {
                ["morning"] = timeOfDay == "morning" ? 1.0f : 0.5f,
                ["afternoon"] = timeOfDay == "afternoon" ? 1.0f : 0.5f,
                ["evening"] = timeOfDay == "evening" ? 1.0f : 0.5f,
                ["weekend"] = dayOfWeek is "saturday" or "sunday" ? 1.0f : 0.3f
            };
        }

        private static float CalculateContextualScore(Place place, string timeOfDay, string dayOfWeek, Dictionary<string, float> preferences)
        {
            var score = 0.5f; // Base score

            if (place.Category?.Contains("restaurant") == true)
            {
                score += timeOfDay switch
                {
                    "morning" => 0.2f,
                    "afternoon" => 0.4f,
                    "evening" => 0.8f,
                    _ => 0.3f
                };
            }

            if (place.Category?.Contains("museum") == true)
            {
                score += timeOfDay switch
                {
                    "morning" => 0.8f,
                    "afternoon" => 0.6f,
                    "evening" => 0.2f,
                    _ => 0.4f
                };
            }

            return Math.Min(score, 1.0f);
        }

        private async Task<Dictionary<string, int>> GetUserCategoryExperienceAsync(Guid userId, CancellationToken cancellationToken)
        {
            var visits = await _context.UserVisits
                .AsNoTracking()
                .Where(v => v.UserId == userId)
                .ToListAsync(cancellationToken);

            var experience = new Dictionary<string, int>();

            foreach (var visit in visits)
            {
                var place = await _context.Places
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == visit.PlaceId, cancellationToken);

                if (place?.Category != null)
                {
                    var categories = place.Category.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var category in categories)
                    {
                        var cleanCategory = category.Trim().ToLower();
                        experience[cleanCategory] = experience.GetValueOrDefault(cleanCategory, 0) + 1;
                    }
                }
            }

            return experience;
        }

        private async Task<float> CalculateLocationNoveltyAsync(Guid userId, Place place, CancellationToken cancellationToken)
        {
            var userVisits = await _context.UserVisits
                .AsNoTracking()
                .Where(v => v.UserId == userId)
                .ToListAsync(cancellationToken);

            if (!userVisits.Any()) return 1.0f; // Maximum novelty for new users

            var avgDistance = 0.0;
            foreach (var visit in userVisits)
            {
                var distance = CalculateDistance(place.Latitude, place.Longitude, visit.Latitude, visit.Longitude);
                avgDistance += distance;
            }
            avgDistance /= userVisits.Count;

            // Novel if average distance > 2km from usual places
            return Math.Min((float)(avgDistance / 2000.0), 1.0f);
        }

        private static float CalculateCategoryDiversityScore(Place place, List<Place> allPlaces)
        {
            if (place.Category == null) return 0.5f;

            var placeCategories = place.Category.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim().ToLower()).ToHashSet();

            var similarCount = allPlaces.Count(p => 
                p.Category != null && 
                p.Category.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Any(c => placeCategories.Contains(c.Trim().ToLower())));

            // More unique = higher diversity score
            return Math.Max(0f, 1f - (similarCount / (float)allPlaces.Count));
        }

        private async Task<float> CalculateDistanceVarietyAsync(Guid userId, Place place, CancellationToken cancellationToken)
        {
            var recentVisits = await _context.UserVisits
                .AsNoTracking()
                .Where(v => v.UserId == userId && v.VisitDate >= DateTime.UtcNow.AddDays(-7))
                .ToListAsync(cancellationToken);

            if (!recentVisits.Any()) return 1.0f;

            var minDistance = recentVisits
                .Min(v => CalculateDistance(place.Latitude, place.Longitude, v.Latitude, v.Longitude));

            // Variety bonus for places >500m from recent visits
            return Math.Min((float)(minDistance / 500.0), 1.0f);
        }

        private static float CalculateTimeAppropriatenessScore(Place place, int currentHour)
        {
            if (place.Category == null) return 0.5f;

            var categories = place.Category.ToLower();
            
            return (currentHour, categories) switch
            {
                (>= 6 and < 11, var c) when c.Contains("cafe") || c.Contains("bakery") => 1.0f,
                (>= 11 and < 15, var c) when c.Contains("restaurant") => 1.0f,
                (>= 9 and < 17, var c) when c.Contains("museum") || c.Contains("tourist") => 1.0f,
                (>= 18 and < 24, var c) when c.Contains("bar") || c.Contains("nightlife") => 1.0f,
                _ => 0.6f
            };
        }

        private static string GetSeason(int month)
        {
            return month switch
            {
                12 or 1 or 2 => "winter",
                3 or 4 or 5 => "spring",
                6 or 7 or 8 => "summer",
                9 or 10 or 11 => "autumn",
                _ => "unknown"
            };
        }

        private static bool IsSeasonallyRelevant(Place place, string season)
        {
            if (place.Category == null) return true;

            var categories = place.Category.ToLower();
            
            return season switch
            {
                "summer" => categories.Contains("beach") || categories.Contains("park") || categories.Contains("outdoor"),
                "winter" => categories.Contains("museum") || categories.Contains("indoor") || categories.Contains("shopping"),
                "spring" => categories.Contains("garden") || categories.Contains("park") || categories.Contains("nature"),
                "autumn" => categories.Contains("museum") || categories.Contains("cultural") || categories.Contains("historic"),
                _ => true
            };
        }

        private static double CalculateDistance(float lat1, float lon1, float lat2, float lon2)
        {
            const double R = 6371e3; // Earth's radius in meters
            var φ1 = lat1 * Math.PI / 180;
            var φ2 = lat2 * Math.PI / 180;
            var Δφ = (lat2 - lat1) * Math.PI / 180;
            var Δλ = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                    Math.Cos(φ1) * Math.Cos(φ2) *
                    Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }
    }
}