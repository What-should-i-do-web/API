using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.Models;
using WhatShouldIDo.Application.Services;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Generates human-readable explanations for recommendation scores.
    /// Maps scoring contributions to stable reason codes with localized messages.
    /// </summary>
    public class ExplainabilityService : IExplainabilityService
    {
        private readonly IPlaceCategoryMapper _categoryMapper;
        private readonly ILocalizationService _localizationService;

        public ExplainabilityService(
            IPlaceCategoryMapper categoryMapper,
            ILocalizationService localizationService)
        {
            _categoryMapper = categoryMapper;
            _localizationService = localizationService;
        }

        public List<RecommendationReasonDto> GenerateReasons(
            Place place,
            ScoreBreakdown breakdown,
            ScoringContext context)
        {
            var reasons = new List<RecommendationReasonDto>();

            // Identify top contributing dimensions
            var contributions = new List<(string dimension, double contribution, double score)>
            {
                ("explicit", breakdown.ExplicitContribution, breakdown.ExplicitScore),
                ("implicit", breakdown.ImplicitContribution, breakdown.ImplicitScore),
                ("novelty", breakdown.NoveltyContribution, breakdown.NoveltyScore),
                ("context", breakdown.ContextContribution, breakdown.ContextScore),
                ("quality", breakdown.QualityContribution, breakdown.QualityScore)
            };

            // Sort by contribution (highest first)
            var topContributors = contributions
                .Where(c => c.contribution > 0.05) // Only meaningful contributions
                .OrderByDescending(c => c.contribution)
                .Take(4) // Max 4 reasons
                .ToList();

            // Generate reasons for each top contributor
            foreach (var (dimension, contribution, score) in topContributors)
            {
                var dimensionReasons = dimension switch
                {
                    "explicit" => GenerateExplicitReasons(place, score, context),
                    "implicit" => GenerateImplicitReasons(place, score, context),
                    "novelty" => GenerateNoveltyReasons(place, score),
                    "context" => GenerateContextReasons(place, score, context),
                    "quality" => GenerateQualityReasons(place, score, context),
                    _ => new List<RecommendationReasonDto>()
                };

                reasons.AddRange(dimensionReasons);
            }

            // If we have too few reasons, add fallback
            if (reasons.Count < 2)
            {
                reasons.Add(CreateReason("GENERAL_RECOMMENDATION", new { name = place.Name }));
            }

            // Limit to 2-4 reasons as per spec
            return reasons.Take(4).ToList();
        }

        /// <summary>
        /// Generate reasons from explicit taste profile matching.
        /// </summary>
        private List<RecommendationReasonDto> GenerateExplicitReasons(
            Place place,
            double score,
            ScoringContext context)
        {
            var reasons = new List<RecommendationReasonDto>();

            if (string.IsNullOrWhiteSpace(place.Category))
                return reasons;

            // Get place interests
            var placeInterests = _categoryMapper.MapToInterests(place.Category);

            if (!placeInterests.Any())
                return reasons;

            // Get user's top interests from profile
            var userInterests = context.TasteProfile?.GetInterestWeights() ?? new Dictionary<string, double>();

            // Find matching interests with high user weight
            foreach (var (interest, placeWeight) in placeInterests.OrderByDescending(kvp => kvp.Value))
            {
                if (userInterests.TryGetValue(interest, out var userWeight) && userWeight > 0.6)
                {
                    reasons.Add(CreateReason(
                        $"MATCHES_INTEREST_{interest.ToUpperInvariant()}",
                        new { interest, name = place.Name }
                    ));
                    break; // Only one interest match reason
                }
            }

            // Check preference dimensions
            if (context.TasteProfile != null)
            {
                var prefs = context.TasteProfile.GetPreferenceWeights();

                // High taste quality preference + high rating
                if (prefs.TryGetValue("TasteQuality", out var tasteQuality) &&
                    tasteQuality > 0.7 &&
                    double.TryParse(place.Rating, out var rating) && rating >= 4.5)
                {
                    reasons.Add(CreateReason("HIGH_TASTE_QUALITY", new { name = place.Name }));
                }

                // Calmness preference
                if (prefs.TryGetValue("Calmness", out var calmness) && calmness > 0.7)
                {
                    var category = place.Category?.ToLower() ?? "";
                    if (category.Contains("park") || category.Contains("library") ||
                        category.Contains("spa") || category.Contains("wellness"))
                    {
                        reasons.Add(CreateReason("CALM_ATMOSPHERE", new { name = place.Name }));
                    }
                }
            }

            return reasons;
        }

        /// <summary>
        /// Generate reasons from implicit learning (visit history).
        /// </summary>
        private List<RecommendationReasonDto> GenerateImplicitReasons(
            Place place,
            double score,
            ScoringContext context)
        {
            var reasons = new List<RecommendationReasonDto>();

            // If high implicit score, it matches their history
            if (score > 0.7)
            {
                reasons.Add(CreateReason("MATCHES_YOUR_HISTORY", new { name = place.Name }));
            }

            // Check if category matches favorite cuisines
            if (context.ImplicitPreferences != null &&
                !string.IsNullOrWhiteSpace(place.Category))
            {
                var category = place.Category.ToLower();

                foreach (var favCuisine in context.ImplicitPreferences.FavoriteCuisines.Take(3))
                {
                    if (category.Contains(favCuisine.ToLower()))
                    {
                        reasons.Add(CreateReason(
                            "FAVORITE_CUISINE",
                            new { cuisine = favCuisine, name = place.Name }
                        ));
                        break;
                    }
                }

                foreach (var favActivity in context.ImplicitPreferences.FavoriteActivityTypes.Take(3))
                {
                    if (category.Contains(favActivity.ToLower()))
                    {
                        reasons.Add(CreateReason(
                            "FAVORITE_ACTIVITY",
                            new { activity = favActivity, name = place.Name }
                        ));
                        break;
                    }
                }
            }

            return reasons;
        }

        /// <summary>
        /// Generate reasons from novelty/exploration scoring.
        /// </summary>
        private List<RecommendationReasonDto> GenerateNoveltyReasons(
            Place place,
            double score)
        {
            var reasons = new List<RecommendationReasonDto>();

            if (score > 0.7)
            {
                // High novelty = trying something new
                reasons.Add(CreateReason("TRY_SOMETHING_NEW", new { name = place.Name }));
            }
            else if (score > 0.5)
            {
                // Medium novelty = explore new area
                reasons.Add(CreateReason("EXPLORE_NEW_AREA", new { name = place.Name }));
            }

            return reasons;
        }

        /// <summary>
        /// Generate reasons from contextual appropriateness (time, weather, etc).
        /// </summary>
        private List<RecommendationReasonDto> GenerateContextReasons(
            Place place,
            double score,
            ScoringContext context)
        {
            var reasons = new List<RecommendationReasonDto>();

            if (score > 0.6)
            {
                var category = place.Category?.ToLower() ?? "";

                // Time-based appropriateness
                var hour = DateTime.UtcNow.Hour;

                if (hour >= 6 && hour < 11 && (category.Contains("cafe") || category.Contains("bakery")))
                {
                    reasons.Add(CreateReason("PERFECT_FOR_MORNING", new { name = place.Name }));
                }
                else if (hour >= 11 && hour < 14 && category.Contains("restaurant"))
                {
                    reasons.Add(CreateReason("PERFECT_FOR_LUNCH", new { name = place.Name }));
                }
                else if (hour >= 18 && hour < 23 && (category.Contains("bar") || category.Contains("nightlife")))
                {
                    reasons.Add(CreateReason("PERFECT_FOR_EVENING", new { name = place.Name }));
                }

                // Weather-based appropriateness
                if (category.Contains("park") || category.Contains("nature") || category.Contains("outdoor"))
                {
                    reasons.Add(CreateReason("ENJOY_OUTDOORS", new { name = place.Name }));
                }
                else if (category.Contains("museum") || category.Contains("gallery") || category.Contains("indoor"))
                {
                    reasons.Add(CreateReason("INDOOR_COMFORT", new { name = place.Name }));
                }
            }

            return reasons;
        }

        /// <summary>
        /// Generate reasons from quality metrics (rating, distance).
        /// </summary>
        private List<RecommendationReasonDto> GenerateQualityReasons(
            Place place,
            double score,
            ScoringContext context)
        {
            var reasons = new List<RecommendationReasonDto>();

            // High rating
            if (double.TryParse(place.Rating, out var rating) && rating >= 4.5)
            {
                var reviewCount = place.ReviewCount ?? 0;

                if (reviewCount >= 100)
                {
                    reasons.Add(CreateReason(
                        "HIGHLY_RATED_POPULAR",
                        new { rating = rating.ToString("0.0"), count = reviewCount }
                    ));
                }
                else if (reviewCount >= 20)
                {
                    reasons.Add(CreateReason(
                        "HIGHLY_RATED",
                        new { rating = rating.ToString("0.0") }
                    ));
                }
            }

            // Close distance
            if (context.Origin.HasValue && place.Latitude != 0 && place.Longitude != 0)
            {
                var distance = CalculateDistance(
                    context.Origin.Value.Latitude,
                    context.Origin.Value.Longitude,
                    place.Latitude,
                    place.Longitude);

                if (distance <= 500) // Within 500m
                {
                    reasons.Add(CreateReason(
                        "VERY_CLOSE",
                        new { distance = $"{(int)distance}m" }
                    ));
                }
                else if (distance <= 1000) // Within 1km
                {
                    reasons.Add(CreateReason(
                        "CLOSE_BY",
                        new { distance = $"{distance / 1000:0.0}km" }
                    ));
                }
            }

            return reasons;
        }

        /// <summary>
        /// Create a reason DTO with localized message.
        /// </summary>
        private RecommendationReasonDto CreateReason(string reasonCode, object? parameters = null)
        {
            // Build localization key
            var localizationKey = $"Recommendation.Reason.{reasonCode}";

            // Get localized message (falls back to key if not found)
            var message = _localizationService.GetString(localizationKey);

            // If parameters provided, attempt simple string interpolation
            if (parameters != null && message.Contains("{"))
            {
                var props = parameters.GetType().GetProperties();
                foreach (var prop in props)
                {
                    var value = prop.GetValue(parameters)?.ToString() ?? "";
                    message = message.Replace($"{{{prop.Name}}}", value);
                }
            }

            return new RecommendationReasonDto
            {
                ReasonCode = reasonCode,
                Message = message
            };
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

        public string GetReasonMessage(string reasonCode)
        {
            var localizationKey = $"Recommendation.Reason.{reasonCode}";
            return _localizationService.GetString(localizationKey);
        }

        public Dictionary<string, string> GetAllReasonCodes()
        {
            var reasonCodes = new Dictionary<string, string>
            {
                // Interest matches
                ["MATCHES_INTEREST_CULTURE"] = "Matches your interest in culture",
                ["MATCHES_INTEREST_FOOD"] = "Matches your interest in food",
                ["MATCHES_INTEREST_NATURE"] = "Matches your interest in nature",
                ["MATCHES_INTEREST_NIGHTLIFE"] = "Matches your interest in nightlife",
                ["MATCHES_INTEREST_SHOPPING"] = "Matches your interest in shopping",
                ["MATCHES_INTEREST_ART"] = "Matches your interest in art",
                ["MATCHES_INTEREST_WELLNESS"] = "Matches your interest in wellness",
                ["MATCHES_INTEREST_SPORTS"] = "Matches your interest in sports",

                // Preferences
                ["HIGH_TASTE_QUALITY"] = "High quality based on your preferences",
                ["CALM_ATMOSPHERE"] = "Offers the calm atmosphere you prefer",

                // History
                ["MATCHES_YOUR_HISTORY"] = "Similar to places you've liked before",
                ["FAVORITE_CUISINE"] = "Features your favorite cuisine",
                ["FAVORITE_ACTIVITY"] = "Offers your favorite activity",

                // Novelty
                ["TRY_SOMETHING_NEW"] = "Something new to explore",
                ["EXPLORE_NEW_AREA"] = "Discover a new area",

                // Context
                ["PERFECT_FOR_MORNING"] = "Perfect for morning time",
                ["PERFECT_FOR_LUNCH"] = "Great for lunch",
                ["PERFECT_FOR_EVENING"] = "Ideal for evening",
                ["ENJOY_OUTDOORS"] = "Enjoy the outdoors",
                ["INDOOR_COMFORT"] = "Comfortable indoor space",

                // Quality
                ["HIGHLY_RATED_POPULAR"] = "Highly rated and popular",
                ["HIGHLY_RATED"] = "Highly rated by visitors",
                ["VERY_CLOSE"] = "Very close to you",
                ["CLOSE_BY"] = "Nearby location",

                // Fallback
                ["GENERAL_RECOMMENDATION"] = "Recommended for you"
            };

            return reasonCodes;
        }
    }
}
