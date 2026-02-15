using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Services;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Enforces intent-based policies on suggestions.
    /// Ensures FOOD_ONLY doesn't build routes, ROUTE_PLANNING has diversity, etc.
    /// </summary>
    public class SuggestionPolicyService : ISuggestionPolicy
    {
        private readonly ILogger<SuggestionPolicyService> _logger;

        public SuggestionPolicyService(ILogger<SuggestionPolicyService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<string>> ValidateRequestAsync(
            SuggestionIntent intent,
            (double latitude, double longitude) location,
            int radiusMeters,
            int? walkingDistance)
        {
            var errors = new List<string>();

            await Task.CompletedTask; // Keep async for future validation logic

            // Validate coordinates
            if (location.latitude < -90 || location.latitude > 90)
            {
                errors.Add("Latitude must be between -90 and 90");
            }

            if (location.longitude < -180 || location.longitude > 180)
            {
                errors.Add("Longitude must be between -180 and 180");
            }

            // Validate radius
            if (radiusMeters < 100 || radiusMeters > 50000)
            {
                errors.Add("Radius must be between 100 and 50,000 meters");
            }

            // Intent-specific validation
            if (intent == SuggestionIntent.ROUTE_PLANNING)
            {
                if (!walkingDistance.HasValue || walkingDistance.Value < 500)
                {
                    errors.Add("Route planning requires a walking distance of at least 500 meters");
                }

                if (walkingDistance.HasValue && walkingDistance.Value > 10000)
                {
                    errors.Add("Walking distance cannot exceed 10,000 meters (10 km)");
                }
            }

            if (errors.Any())
            {
                _logger.LogWarning("Intent validation failed for {Intent}: {Errors}",
                    intent, string.Join(", ", errors));
            }

            return errors;
        }

        public async Task<List<Place>> ApplyIntentFilterAsync(
            SuggestionIntent intent,
            List<Place> places,
            List<string> userExclusions)
        {
            await Task.CompletedTask; // Keep async for future filter logic

            if (!intent.HasCategoryRestrictions())
            {
                // No filtering needed for unrestricted intents
                return places.Where(p => !userExclusions.Contains(p.Id.ToString())).ToList();
            }

            var allowedCategories = intent.GetAllowedCategories();
            var filtered = places.Where(place =>
            {
                // Exclude user-excluded places
                if (userExclusions.Contains(place.Id.ToString()))
                {
                    return false;
                }

                // Check if place category matches allowed categories
                if (string.IsNullOrWhiteSpace(place.Category))
                {
                    return false;
                }

                var placeCategories = place.Category.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim().ToLowerInvariant());

                return placeCategories.Any(pc => allowedCategories.Any(ac => pc.Contains(ac.ToLowerInvariant())));
            }).ToList();

            if (filtered.Count < places.Count)
            {
                _logger.LogInformation("Intent filter {Intent} reduced {Original} places to {Filtered} places",
                    intent, places.Count, filtered.Count);
            }

            return filtered;
        }

        public bool ShouldBuildRoute(SuggestionIntent intent)
        {
            return intent.RequiresRoute();
        }

        public double GetDiversityFactor(SuggestionIntent intent)
        {
            return intent switch
            {
                SuggestionIntent.QUICK_SUGGESTION => 0.3,  // Low diversity
                SuggestionIntent.FOOD_ONLY => 0.5,          // Medium diversity (different cuisines)
                SuggestionIntent.ACTIVITY_ONLY => 0.6,      // Medium-high diversity
                SuggestionIntent.ROUTE_PLANNING => 0.8,     // High diversity (varied day)
                SuggestionIntent.TRY_SOMETHING_NEW => 1.0,  // Maximum diversity
                _ => 0.5
            };
        }

        public int GetMaxWalkingDistance(SuggestionIntent intent, int? userPreference)
        {
            // User preference takes priority
            if (userPreference.HasValue)
            {
                return Math.Min(userPreference.Value, 10000); // Cap at 10km
            }

            // Default based on intent
            return intent switch
            {
                SuggestionIntent.QUICK_SUGGESTION => 1000,   // 1 km
                SuggestionIntent.FOOD_ONLY => 2000,          // 2 km
                SuggestionIntent.ACTIVITY_ONLY => 3000,      // 3 km
                SuggestionIntent.ROUTE_PLANNING => 5000,     // 5 km
                SuggestionIntent.TRY_SOMETHING_NEW => 4000,  // 4 km
                _ => 3000
            };
        }

        public async Task<List<string>> GenerateReasonsAsync(
            SuggestionIntent intent,
            Place place,
            (double latitude, double longitude) userLocation,
            List<string> matchedPreferences,
            double noveltyScore,
            List<string> contextualReasons)
        {
            await Task.CompletedTask; // Keep async for future enhancements

            var reasons = new List<string>();

            // Distance reason (always relevant)
            var distance = CalculateDistance(userLocation, (place.Latitude, place.Longitude));
            if (distance < 500)
            {
                reasons.Add("Very close to you (walking distance)");
            }
            else if (distance < 1500)
            {
                reasons.Add("Close to your location");
            }

            // Quality reason (rating)
            if (!string.IsNullOrWhiteSpace(place.Rating))
            {
                if (double.TryParse(place.Rating, out var rating))
                {
                    if (rating >= 4.5)
                    {
                        reasons.Add("Highly rated (4.5+ stars)");
                    }
                    else if (rating >= 4.0)
                    {
                        reasons.Add("Well-rated");
                    }
                }
            }

            // Intent-specific reasons
            switch (intent)
            {
                case SuggestionIntent.FOOD_ONLY:
                    reasons.Add("Matches your food preference");
                    break;

                case SuggestionIntent.ACTIVITY_ONLY:
                    reasons.Add("Great activity for your area");
                    break;

                case SuggestionIntent.ROUTE_PLANNING:
                    reasons.Add("Part of a balanced day plan");
                    break;

                case SuggestionIntent.TRY_SOMETHING_NEW:
                    if (noveltyScore > 0.7)
                    {
                        reasons.Add("A new experience for you");
                    }
                    else if (noveltyScore > 0.4)
                    {
                        reasons.Add("Something different but familiar");
                    }
                    break;
            }

            // Preference matches
            if (matchedPreferences.Any())
            {
                reasons.Add($"Matches your interests: {string.Join(", ", matchedPreferences.Take(2))}");
            }

            // Contextual reasons (weather, time, season)
            reasons.AddRange(contextualReasons.Take(2));

            // Limit to 5 reasons max
            return reasons.Take(5).ToList();
        }

        private double CalculateDistance(
            (double latitude, double longitude) point1,
            (double latitude, double longitude) point2)
        {
            const double earthRadiusMeters = 6371000;

            var lat1Rad = point1.latitude * Math.PI / 180.0;
            var lat2Rad = point2.latitude * Math.PI / 180.0;
            var deltaLat = (point2.latitude - point1.latitude) * Math.PI / 180.0;
            var deltaLon = (point2.longitude - point1.longitude) * Math.PI / 180.0;

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusMeters * c;
        }
    }
}
