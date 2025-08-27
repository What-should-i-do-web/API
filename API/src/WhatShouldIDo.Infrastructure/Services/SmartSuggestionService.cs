using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class SmartSuggestionService : ISmartSuggestionService
    {
        private readonly IPlacesProvider _placesProvider;
        private readonly IPromptInterpreter _promptInterpreter;
        private readonly IGeocodingService _geocodingService;
        private readonly IVisitTrackingService _visitTrackingService;
        private readonly IPreferenceLearningService _preferenceLearningService;
        private readonly IVariabilityEngine _variabilityEngine;
        private readonly IContextEngine _contextEngine;
        private readonly ILogger<SmartSuggestionService> _logger;

        public SmartSuggestionService(
            IPlacesProvider placesProvider,
            IPromptInterpreter promptInterpreter,
            IGeocodingService geocodingService,
            IVisitTrackingService visitTrackingService,
            IPreferenceLearningService preferenceLearningService,
            IVariabilityEngine variabilityEngine,
            IContextEngine contextEngine,
            ILogger<SmartSuggestionService> logger)
        {
            _placesProvider = placesProvider;
            _promptInterpreter = promptInterpreter;
            _geocodingService = geocodingService;
            _visitTrackingService = visitTrackingService;
            _preferenceLearningService = preferenceLearningService;
            _variabilityEngine = variabilityEngine;
            _contextEngine = contextEngine;
            _logger = logger;
        }

        public async Task<List<SuggestionDto>> GetPersonalizedSuggestionsAsync(Guid userId, PromptRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var interpreted = await _promptInterpreter.InterpretAsync(request.Prompt);
                float lat = request.Latitude ?? 0;
                float lng = request.Longitude ?? 0;

                // Get coordinates if location text provided
                if (!string.IsNullOrWhiteSpace(interpreted.LocationText))
                {
                    (lat, lng) = await _geocodingService.GetCoordinatesAsync(interpreted.LocationText);
                }

                // Get base places from provider
                var places = await _placesProvider.SearchByPromptAsync(
                    interpreted.TextQuery,
                    lat,
                    lng,
                    interpreted.PricePreferences
                );

                // Apply personalization
                var personalizedSuggestions = await ApplyPersonalizationAsync(userId, places, request.Prompt, cancellationToken);

                // Log suggestion views for learning
                foreach (var suggestion in personalizedSuggestions.Take(5)) // Log top 5 viewed
                {
                    var place = places.FirstOrDefault(p => p.Name == suggestion.PlaceName);
                    if (place != null)
                    {
                        await _visitTrackingService.LogSuggestionViewAsync(userId, place, suggestion.Reason, cancellationToken);
                    }
                }

                _logger.LogInformation("Generated {Count} personalized suggestions for user {UserId}", 
                    personalizedSuggestions.Count, userId);

                return personalizedSuggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating personalized suggestions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<SuggestionDto>> GetPersonalizedNearbySuggestionsAsync(Guid userId, float lat, float lng, int radius, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get base nearby places
                var places = await _placesProvider.GetNearbyPlacesAsync(lat, lng, radius);

                // Apply personalization
                var personalizedSuggestions = await ApplyPersonalizationAsync(userId, places, "yakın konumdan öneriler", cancellationToken);

                // Log interactions
                foreach (var suggestion in personalizedSuggestions.Take(5))
                {
                    var place = places.FirstOrDefault(p => p.Name == suggestion.PlaceName);
                    if (place != null)
                    {
                        await _visitTrackingService.LogSuggestionViewAsync(userId, place, "Yakın konumdan önerildi", cancellationToken);
                    }
                }

                return personalizedSuggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating personalized nearby suggestions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<SuggestionDto> GetPersonalizedRandomSuggestionAsync(Guid userId, float lat, float lng, int radius, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get personalized nearby suggestions
                var suggestions = await GetPersonalizedNearbySuggestionsAsync(userId, lat, lng, radius, cancellationToken);

                if (!suggestions.Any())
                    return null;

                // Apply smart randomization - prefer high-scoring personalized suggestions
                var weightedRandom = ApplySmartRandomization(suggestions);
                return weightedRandom;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating personalized random suggestion for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<SuggestionDto>> ApplyPersonalizationAsync(Guid userId, List<Place> places, string originalPrompt, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!places.Any()) return new List<SuggestionDto>();

                // Step 1: Apply contextual filtering (NEW - Weather, Time, Location context)
                var contextuallyFiltered = await _contextEngine.ApplyContextualFiltering(places, places.FirstOrDefault()?.Latitude ?? 0, places.FirstOrDefault()?.Longitude ?? 0, cancellationToken);
                
                // Step 2: Apply variability filtering to avoid repetition
                var filteredPlaces = await _variabilityEngine.FilterForVarietyAsync(userId, contextuallyFiltered, cancellationToken);

                // Step 3: Apply discovery boost for new experiences  
                filteredPlaces = await _variabilityEngine.ApplyDiscoveryBoostAsync(userId, filteredPlaces, cancellationToken);

                // Step 4: Apply seasonal variety
                filteredPlaces = await _variabilityEngine.ApplySeasonalVarietyAsync(filteredPlaces, cancellationToken);

                // Step 5: Apply contextual variety (time/day)
                var timeOfDay = GetTimeOfDay(DateTime.Now);
                var dayOfWeek = DateTime.Now.DayOfWeek.ToString().ToLower();
                filteredPlaces = await _variabilityEngine.ApplyContextualVarietyAsync(userId, filteredPlaces, timeOfDay, dayOfWeek, cancellationToken);

                // Step 6: Get user preferences for scoring
                var userPreferences = await _preferenceLearningService.GetLearnedPreferencesAsync(userId, cancellationToken);

                // Step 7: Score and rank places
                var scoredSuggestions = new List<(SuggestionDto suggestion, float personalizedScore)>();

                foreach (var place in filteredPlaces)
                {
                    var suggestion = await CreateSuggestionFromPlaceAsync(place, originalPrompt, cancellationToken);
                    var personalizedScore = await CalculatePersonalizedScoreAsync(userId, place, userPreferences, cancellationToken);
                    
                    suggestion.Score = personalizedScore;
                    scoredSuggestions.Add((suggestion, personalizedScore));
                }

                // Step 8: Final ranking with personalization
                var finalSuggestions = scoredSuggestions
                    .OrderByDescending(s => s.personalizedScore)
                    .ThenByDescending(s => s.suggestion.IsSponsored) // Sponsored content still gets priority
                    .Select(s => s.suggestion)
                    .ToList();

                _logger.LogDebug("Applied personalization: {Original} → {Filtered} → {Final} suggestions for user {UserId}",
                    places.Count, filteredPlaces.Count, finalSuggestions.Count, userId);

                return finalSuggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying personalization for user {UserId}", userId);
                
                // Fallback to basic suggestions on error
                return places.Select(place => new SuggestionDto
                {
                    Id = Guid.NewGuid(),
                    PlaceName = place.Name,
                    Reason = "Varsayılan öneri",
                    Score = 0.5,
                    Latitude = place.Latitude,
                    Longitude = place.Longitude,
                    PhotoReference = place.PhotoReference,
                    PhotoUrl = place.PhotoUrl,
                    Category = place.Category ?? "",
                    Source = place.Source ?? "",
                    CreatedAt = DateTime.UtcNow
                }).ToList();
            }
        }

        public async Task LogSuggestionInteractionAsync(Guid userId, Guid suggestionId, string interactionType, CancellationToken cancellationToken = default)
        {
            try
            {
                // This would log interactions like "clicked", "visited", "rated", etc.
                // For now, we'll implement basic logging
                _logger.LogInformation("User {UserId} interacted with suggestion {SuggestionId}: {InteractionType}", 
                    userId, suggestionId, interactionType);

                // Future: Store in database for learning
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging interaction for user {UserId}", userId);
            }
        }

        // Private helper methods
        private async Task<float> CalculatePersonalizedScoreAsync(Guid userId, Place place, UserPreferences preferences, CancellationToken cancellationToken)
        {
            try
            {
                var score = 0.5f; // Base score

                // Category preference boost
                if (place.Category != null && preferences.FavoriteCuisines.Any())
                {
                    var placeCategories = place.Category.ToLower();
                    foreach (var favCuisine in preferences.FavoriteCuisines)
                    {
                        if (placeCategories.Contains(favCuisine.ToLower()))
                        {
                            score += 0.3f;
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
                            score += 0.2f;
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
                            score -= 0.4f;
                            break;
                        }
                    }
                }

                // Novelty boost
                var noveltyScore = await _variabilityEngine.CalculateNoveltyScoreAsync(userId, place, cancellationToken);
                score += noveltyScore * 0.2f;

                // Avoidance score penalty (recently visited, poorly rated)
                var avoidanceScore = await _visitTrackingService.GetPlaceAvoidanceScoreAsync(userId, place, cancellationToken);
                score -= avoidanceScore * 0.3f;

                // Original rating factor
                if (place.Rating != null && float.TryParse(place.Rating, out var rating))
                {
                    score += (rating / 5f) * 0.1f; // 10% weight for original rating
                }

                return Math.Max(0f, Math.Min(score, 5f)); // Clamp between 0-5
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating personalized score");
                return 2.5f; // Neutral score on error
            }
        }

        private async Task<SuggestionDto> CreateSuggestionFromPlaceAsync(Place place, string originalPrompt, CancellationToken cancellationToken)
        {
            // Get contextual insights for intelligent reasoning
            var contextualInsight = await _contextEngine.GetContextualInsights(place.Latitude, place.Longitude, cancellationToken);
            var contextualReasons = await _contextEngine.GetContextualReasons(place, contextualInsight);
            
            // Generate enhanced reason with context
            var reason = await GenerateEnhancedPersonalizedReason(place, originalPrompt, contextualReasons);

            return new SuggestionDto
            {
                Id = Guid.NewGuid(),
                PlaceName = place.Name,
                Reason = reason,
                Score = place.Rating != null && double.TryParse(place.Rating, out var rating) ? rating : 3.0,
                IsSponsored = place.IsSponsored,
                SponsoredUntil = place.SponsoredUntil,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                PhotoReference = place.PhotoReference,
                PhotoUrl = place.PhotoUrl,
                Category = place.Category ?? "",
                Source = place.Source ?? "",
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task<string> GenerateEnhancedPersonalizedReason(Place place, string originalPrompt, List<string> contextualReasons)
        {
            var reasons = new List<string>();

            // Priority 1: Original prompt match
            if (!string.IsNullOrWhiteSpace(originalPrompt) && originalPrompt != "yakın konumdan öneriler")
            {
                reasons.Add($"'{originalPrompt}' aramanıza uygun");
            }

            // Priority 2: Contextual reasons (weather, time, season)
            if (contextualReasons.Any())
            {
                reasons.AddRange(contextualReasons);
            }

            // Priority 3: Fallback to basic time-based reason if no contextual reasons
            if (!reasons.Any())
            {
                reasons.Add(GenerateBasicTimeReason(place));
            }

            // Return the most relevant reason (limit to avoid long text)
            return reasons.FirstOrDefault() ?? "Size uygun bir öneri";
        }

        private static string GenerateBasicTimeReason(Place place)
        {
            var timeOfDay = GetTimeOfDay(DateTime.Now);
            var placeName = place.Name.ToLower();

            if (placeName.Contains("cafe") || placeName.Contains("kahve"))
            {
                return timeOfDay switch
                {
                    "morning" => "Sabah kahvesi için ideal",
                    "afternoon" => "Öğleden sonra molası için uygun",
                    _ => "Kahve molası için harika"
                };
            }

            if (placeName.Contains("restaurant") || placeName.Contains("restoran"))
            {
                return timeOfDay switch
                {
                    "morning" => "Kahvaltı için ideal",
                    "afternoon" => "Öğle yemeği için uygun",
                    "evening" => "Akşam yemeği için harika",
                    _ => "Yemek için güzel seçenek"
                };
            }

            return "Yakın konumdan önerildi";
        }

        private static string GeneratePersonalizedReason(Place place, string originalPrompt)
        {
            var timeOfDay = GetTimeOfDay(DateTime.Now);
            var season = GetCurrentSeason();

            // Generate context-aware reasons
            var reasons = new List<string>();

            if (!string.IsNullOrWhiteSpace(originalPrompt) && originalPrompt != "yakın konumdan öneriler")
            {
                reasons.Add($"'{originalPrompt}' aramanızla eşleşiyor");
            }

            if (place.Category?.Contains("restaurant") == true)
            {
                reasons.Add(timeOfDay switch
                {
                    "morning" => "Kahvaltı için ideal",
                    "afternoon" => "Öğle yemeği için uygun", 
                    "evening" => "Akşam yemeği için harika",
                    _ => "Lezzetli seçenekler sunuyor"
                });
            }
            else if (place.Category?.Contains("museum") == true)
            {
                reasons.Add(timeOfDay switch
                {
                    "morning" => "Sabah sakinliğinde keşfetmek için",
                    "afternoon" => "Öğleden sonra kültür turu için",
                    _ => "Kültürel deneyim için mükemmel"
                });
            }

            if (place.Rating != null && float.TryParse(place.Rating, out var rating) && rating >= 4.0f)
            {
                reasons.Add("Yüksek puanla öne çıkıyor");
            }

            return reasons.Any() ? reasons.First() : "Sizin için özenle seçildi";
        }

        private static SuggestionDto ApplySmartRandomization(List<SuggestionDto> suggestions)
        {
            // Apply weighted randomization - higher scores get higher probability
            var random = new Random();
            var totalWeight = suggestions.Sum(s => s.Score + 1.0); // +1 to avoid zero weights

            var randomValue = random.NextDouble() * totalWeight;
            var currentWeight = 0.0;

            foreach (var suggestion in suggestions)
            {
                currentWeight += suggestion.Score + 1.0;
                if (randomValue <= currentWeight)
                {
                    suggestion.Reason = "Sizin için rastgele seçildi - " + suggestion.Reason;
                    return suggestion;
                }
            }

            // Fallback to first suggestion
            return suggestions.First();
        }

        private static string GetTimeOfDay(DateTime dateTime)
        {
            var hour = dateTime.Hour;
            return hour switch
            {
                >= 5 and < 12 => "morning",
                >= 12 and < 17 => "afternoon",
                >= 17 and < 22 => "evening", 
                _ => "night"
            };
        }

        private static string GetCurrentSeason()
        {
            var month = DateTime.Now.Month;
            return month switch
            {
                12 or 1 or 2 => "winter",
                3 or 4 or 5 => "spring", 
                6 or 7 or 8 => "summer",
                9 or 10 or 11 => "autumn",
                _ => "spring"
            };
        }
    }
}