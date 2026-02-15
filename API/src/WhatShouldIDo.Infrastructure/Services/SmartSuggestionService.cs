using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.Models;
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
        private readonly IUserHistoryRepository _userHistoryRepository;
        private readonly IRouteOptimizationService _routeOptimizationService;
        private readonly IAIService _aiService;
        private readonly IHybridScorer _hybridScorer;
        private readonly ITasteProfileRepository _tasteProfileRepository;
        private readonly ILogger<SmartSuggestionService> _logger;

        public SmartSuggestionService(
            IPlacesProvider placesProvider,
            IPromptInterpreter promptInterpreter,
            IGeocodingService geocodingService,
            IVisitTrackingService visitTrackingService,
            IPreferenceLearningService preferenceLearningService,
            IVariabilityEngine variabilityEngine,
            IContextEngine contextEngine,
            IUserHistoryRepository userHistoryRepository,
            IRouteOptimizationService routeOptimizationService,
            IAIService aiService,
            IHybridScorer hybridScorer,
            ITasteProfileRepository tasteProfileRepository,
            ILogger<SmartSuggestionService> logger)
        {
            _placesProvider = placesProvider;
            _promptInterpreter = promptInterpreter;
            _geocodingService = geocodingService;
            _visitTrackingService = visitTrackingService;
            _preferenceLearningService = preferenceLearningService;
            _variabilityEngine = variabilityEngine;
            _contextEngine = contextEngine;
            _userHistoryRepository = userHistoryRepository;
            _routeOptimizationService = routeOptimizationService;
            _aiService = aiService;
            _hybridScorer = hybridScorer;
            _tasteProfileRepository = tasteProfileRepository;
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

                // Step 6: Prepare scoring context (hybrid personalization)
                var userPreferences = await _preferenceLearningService.GetLearnedPreferencesAsync(userId, cancellationToken);
                var tasteProfile = await _tasteProfileRepository.GetByUserIdAsync(userId, cancellationToken);

                var scoringContext = new Application.Models.ScoringContext
                {
                    Origin = filteredPlaces.Any()
    ? ((double)filteredPlaces.First().Latitude,
       (double)filteredPlaces.First().Longitude)
    : null,

                    ImplicitPreferences = userPreferences,
                    TasteProfile = tasteProfile
                };

                // Step 7: Score and rank places using hybrid scorer
                var scoredPlaces = await _hybridScorer.ScoreAndExplainAsync(userId, filteredPlaces, scoringContext, cancellationToken);

                // Step 8: Convert to SuggestionDto and apply final ranking
                var finalSuggestions = new List<SuggestionDto>();

                foreach (var scoredPlace in scoredPlaces)
                {
                    var suggestion = await CreateSuggestionFromPlaceAsync(scoredPlace.Place, originalPrompt, cancellationToken);
                    suggestion.Score = scoredPlace.Score;
                    suggestion.ExplainabilityReasons = scoredPlace.Reasons; // Add explainability

                    finalSuggestions.Add(suggestion);
                }

                // Final sort by score, then sponsored priority
                finalSuggestions = finalSuggestions
                    .OrderByDescending(s => s.Score)
                    .ThenByDescending(s => s.IsSponsored)
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

        // ============== Surprise Me Implementation ==============

        public async Task<SurpriseMeResponse> GenerateSurpriseRouteAsync(
            Guid userId,
            SurpriseMeRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Generating Surprise Me route for user {UserId} in {Area}", userId, request.TargetArea);

                // Generate session ID for tracking
                var sessionId = Guid.NewGuid().ToString();

                // Step 1: Load user's personalization data
                var (exclusions, favorites, recentlyExcludedIds) = await LoadUserPersonalizationDataAsync(userId, cancellationToken);

                _logger.LogDebug("Loaded {ExclusionCount} exclusions, {FavoriteCount} favorites, {RecentCount} recently suggested places",
                    exclusions.Count, favorites.Count, recentlyExcludedIds.Count);

                // Step 2: Fetch places from target area
                var allPlaces = await FetchPlacesFromAreaAsync(request, cancellationToken);
                _logger.LogDebug("Fetched {PlaceCount} places from {Area}", allPlaces.Count, request.TargetArea);

                // Step 3: Apply hard filters (exclusions, recently suggested window)
                var filteredPlaces = ApplyHardFilters(allPlaces, exclusions, recentlyExcludedIds);
                _logger.LogDebug("After hard filters: {FilteredCount} places remaining", filteredPlaces.Count);

                if (!filteredPlaces.Any())
                {
                    throw new InvalidOperationException("No places available after applying filters. Try expanding the search radius or adjusting exclusions.");
                }

                // Step 4: Apply personalization and AI re-ranking
                var personalizedPlaces = await ApplyPersonalizationAndRankingAsync(userId, filteredPlaces, favorites, request, cancellationToken);
                _logger.LogDebug("After personalization: {PersonalizedCount} places ranked", personalizedPlaces.Count);

                // Step 5: Select diverse set of places
                var selectedPlaces = SelectDiversePlaces(personalizedPlaces, request.MinStops, request.MaxStops);
                _logger.LogDebug("Selected {SelectedCount} diverse places", selectedPlaces.Count);

                // Step 6: Optimize route order
                var optimizedRoute = await OptimizeRouteOrderAsync(selectedPlaces, request, cancellationToken);

                // Step 7: Build response with metadata
                var response = await BuildSurpriseMeResponseAsync(
                    userId,
                    optimizedRoute,
                    selectedPlaces,
                    request,
                    sessionId,
                    cancellationToken);

                // Step 8: Persist to history if requested
                if (request.SaveToHistory)
                {
                    await PersistToHistoryAsync(userId, response, selectedPlaces, sessionId, cancellationToken);
                    response.SavedToHistory = true;
                }

                _logger.LogInformation("Successfully generated Surprise Me route for user {UserId} with {StopCount} stops",
                    userId, selectedPlaces.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Surprise Me route for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Loads user's exclusions, favorites, and recently suggested place IDs
        /// </summary>
        private async Task<(List<string> exclusions, List<string> favorites, List<string> recentlyExcluded)>
            LoadUserPersonalizationDataAsync(Guid userId, CancellationToken cancellationToken)
        {
            // Load active exclusions
            var exclusionEntities = await _userHistoryRepository.GetActiveExclusionsAsync(userId, cancellationToken);
            var exclusions = exclusionEntities.Select(e => e.PlaceId).ToList();

            // Load favorites
            var favoriteEntities = await _userHistoryRepository.GetUserFavoritesAsync(userId, cancellationToken);
            var favorites = favoriteEntities.Select(f => f.PlaceId).ToList();

            // Load recently suggested place IDs (exclusion window)
            // Default exclusion window size is 3 (configurable)
            var recentlyExcluded = (await _userHistoryRepository.GetRecentlyExcludedPlaceIdsAsync(userId, 3, cancellationToken))
                .ToList();

            return (exclusions, favorites, recentlyExcluded);
        }

        /// <summary>
        /// Fetches places from the target area using the places provider
        /// </summary>
        private async Task<List<Place>> FetchPlacesFromAreaAsync(SurpriseMeRequest request, CancellationToken cancellationToken)
        {
            var places = new List<Place>();

            // If preferred categories specified, search for each category
            if (request.PreferredCategories?.Any() == true)
            {
                foreach (var category in request.PreferredCategories)
                {
                    var categoryPlaces = await _placesProvider.SearchByPromptAsync(
                        category,
                        (float)request.Latitude,
                        (float)request.Longitude,
                        null);
                    places.AddRange(categoryPlaces);
                }

                // Remove duplicates
                places = places.GroupBy(p => p.GooglePlaceId).Select(g => g.First()).ToList();
            }
            else
            {
                // Get nearby places (all categories)
                places = await _placesProvider.GetNearbyPlacesAsync(
                    (float)request.Latitude,
                    (float)request.Longitude,
                    request.RadiusMeters);
            }

            return places;
        }

        /// <summary>
        /// Applies hard filters: exclusions and recently suggested window
        /// </summary>
        private List<Place> ApplyHardFilters(List<Place> places, List<string> exclusions, List<string> recentlyExcludedIds)
        {
            return places.Where(p =>
                !exclusions.Contains(p.GooglePlaceId) &&                  // Not in exclusion list
                !recentlyExcludedIds.Contains(p.GooglePlaceId)            // Not recently suggested
            ).ToList();
        }

        /// <summary>
        /// Applies personalization scoring and AI-based ranking
        /// </summary>
        private async Task<List<(Place place, double score)>> ApplyPersonalizationAndRankingAsync(
            Guid userId,
            List<Place> places,
            List<string> favoriteIds,
            SurpriseMeRequest request,
            CancellationToken cancellationToken)
        {
            var userPreferences = await _preferenceLearningService.GetLearnedPreferencesAsync(userId, cancellationToken);
            var scoredPlaces = new List<(Place place, double score)>();

            foreach (var place in places)
            {
                // Calculate base personalization score
                var baseScore = await CalculatePersonalizedScoreAsync(userId, place, userPreferences, cancellationToken);

                // Apply favorite boost
                if (favoriteIds.Contains(place.GooglePlaceId))
                {
                    baseScore += 0.5f; // Strong boost for favorites
                }

                // Apply budget preference
                if (!string.IsNullOrWhiteSpace(request.BudgetLevel) && place.PriceLevel != null)
                {
                    var budgetScore = CalculateBudgetScore(request.BudgetLevel, place.PriceLevel);
                    baseScore += (float)budgetScore * 0.2f;
                }

                scoredPlaces.Add((place, baseScore));
            }

            // Sort by score descending
            return scoredPlaces.OrderByDescending(sp => sp.score).ToList();
        }

        /// <summary>
        /// Selects a diverse set of places from ranked list
        /// </summary>
        private List<Place> SelectDiversePlaces(List<(Place place, double score)> rankedPlaces, int minStops, int maxStops)
        {
            var selectedPlaces = new List<Place>();
            var categoryCount = new Dictionary<string, int>();
            var targetCount = Math.Min(maxStops, rankedPlaces.Count);

            // First pass: Select top-scored places with category diversity
            foreach (var (place, score) in rankedPlaces)
            {
                if (selectedPlaces.Count >= targetCount)
                    break;

                var category = place.Category ?? "other";
                var currentCategoryCount = categoryCount.GetValueOrDefault(category, 0);

                // Limit same category to avoid monotony (max 2 of same category)
                if (currentCategoryCount < 2)
                {
                    selectedPlaces.Add(place);
                    categoryCount[category] = currentCategoryCount + 1;
                }
            }

            // Second pass: Fill remaining slots if we haven't reached minStops
            if (selectedPlaces.Count < minStops)
            {
                var remaining = rankedPlaces
                    .Select(rp => rp.place)
                    .Except(selectedPlaces)
                    .Take(minStops - selectedPlaces.Count);
                selectedPlaces.AddRange(remaining);
            }

            return selectedPlaces;
        }

        /// <summary>
        /// Optimizes the route order using TSP solver
        /// </summary>
        private async Task<OptimizedRoute> OptimizeRouteOrderAsync(
            List<Place> places,
            SurpriseMeRequest request,
            CancellationToken cancellationToken)
        {
            // Convert places to waypoints
            var waypoints = places.Select(p => new RouteWaypoint
            {
                Id = p.GooglePlaceId,
                Name = p.Name,
                Latitude = (double)p.Latitude,
                Longitude = (double)p.Longitude,
                IsMandatory = true
            }).ToList();

            // Use first place as start point
            var startPoint = ((double)places.First().Latitude, (double)places.First().Longitude);

            // Optimize route
            var optimizedRoute = await _routeOptimizationService.OptimizeRouteAsync(
                startPoint,
                waypoints,
                request.TransportationMode,
                cancellationToken);

            return optimizedRoute;
        }

        /// <summary>
        /// Builds the Surprise Me response with all metadata
        /// </summary>
        private async Task<SurpriseMeResponse> BuildSurpriseMeResponseAsync(
            Guid userId,
            OptimizedRoute optimizedRoute,
            List<Place> selectedPlaces,
            SurpriseMeRequest request,
            string sessionId,
            CancellationToken cancellationToken)
        {
            var userFavorites = (await _userHistoryRepository.GetUserFavoritesAsync(userId, cancellationToken))
                .Select(f => f.PlaceId)
                .ToHashSet();

            var userVisits = await _visitTrackingService.GetUserVisitHistoryAsync(userId, days: 90, cancellationToken);
            var visitedPlaceIds = userVisits.Select(v => v.PlaceId).ToHashSet();

            // Build suggested places list
            var suggestedPlaces = new List<SurpriseMePlaceDto>();
            int previousDistance = 0;
            int previousDuration = 0;

            foreach (var optimizedWaypoint in optimizedRoute.OrderedWaypoints)
            {
                var place = selectedPlaces.First(p => p.GooglePlaceId == optimizedWaypoint.Waypoint.Id);

                var placeDto = new SurpriseMePlaceDto
                {
                    PlaceId = place.GooglePlaceId,
                    Name = place.Name,
                    Category = place.Category,
                    Latitude = (double)place.Latitude,
                    Longitude = (double)place.Longitude,
                    Address = place.Address,
                    Rating = place.Rating != null && double.TryParse(place.Rating, out var rating) ? rating : null,
                    PriceLevel = place.PriceLevel != null && int.TryParse(place.PriceLevel, out var priceLevel) ? priceLevel : null,
                    PhotoUrl = place.PhotoUrl,
                    RouteOrder = optimizedWaypoint.OptimizedOrder,
                    Reason = request.IncludeReasoning ? await GenerateAIReasonAsync(place, request, cancellationToken) : null,
                    PersonalizationScore = 0.8, // Placeholder, could be calculated
                    IsFavorite = userFavorites.Contains(place.GooglePlaceId),
                    PreviouslyVisited = visitedPlaceIds.Contains(place.Id),
                    DistanceFromPrevious = optimizedWaypoint.OptimizedOrder > 1 ? (double)optimizedWaypoint.DistanceFromPreviousMeters : null,
                    TravelTimeFromPrevious = optimizedWaypoint.OptimizedOrder > 1 ? optimizedWaypoint.DurationFromPreviousSeconds / 60 : null
                };

                suggestedPlaces.Add(placeDto);
            }

            // Calculate diversity score (0-1, based on unique categories)
            var uniqueCategories = selectedPlaces.Select(p => p.Category).Distinct().Count();
            var diversityScore = Math.Min(1.0, uniqueCategories / (double)selectedPlaces.Count);

            // Build route DTO
            var routeDto = new RouteDto
            {
                Id = Guid.NewGuid(),
                Name = $"Surprise Me - {request.TargetArea}",
                Description = $"AI-generated personalized route in {request.TargetArea}",
                UserId = userId,
                TotalDistance = optimizedRoute.TotalDistanceMeters,
                EstimatedDuration = optimizedRoute.TotalDurationSeconds / 60, // Convert to minutes
                StopCount = selectedPlaces.Count,
                TransportationMode = request.TransportationMode,
                RouteType = "surprise_me",
                Tags = new List<string> { "surprise_me", "ai_generated" },
                IsPublic = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var response = new SurpriseMeResponse
            {
                Route = routeDto,
                SuggestedPlaces = suggestedPlaces,
                Reasoning = request.IncludeReasoning ? GenerateRouteReasoning(selectedPlaces, diversityScore) : null,
                DiversityScore = diversityScore,
                PersonalizationScore = 0.85, // Placeholder, could be calculated as average
                SessionId = sessionId,
                SavedToHistory = false
            };

            return response;
        }

        /// <summary>
        /// Generates AI reasoning for a place suggestion
        /// </summary>
        private Task<string> GenerateAIReasonAsync(Place place, SurpriseMeRequest request, CancellationToken cancellationToken)
        {
            // Generate contextual reason based on place attributes
            var reason = place.Category?.ToLower() switch
            {
                var c when c.Contains("restaurant") || c.Contains("food") => $"Delicious dining in {request.TargetArea}",
                var c when c.Contains("museum") || c.Contains("gallery") => $"Cultural experience in {request.TargetArea}",
                var c when c.Contains("park") || c.Contains("garden") => $"Beautiful outdoor space in {request.TargetArea}",
                var c when c.Contains("cafe") || c.Contains("coffee") => $"Perfect coffee spot in {request.TargetArea}",
                var c when c.Contains("bar") || c.Contains("nightlife") => $"Great nightlife in {request.TargetArea}",
                var c when c.Contains("shopping") || c.Contains("mall") => $"Shopping destination in {request.TargetArea}",
                _ => $"Great spot in {request.TargetArea}"
            };

            return Task.FromResult(reason);
        }

        /// <summary>
        /// Generates overall reasoning for the route
        /// </summary>
        private string GenerateRouteReasoning(List<Place> places, double diversityScore)
        {
            var categories = places.Select(p => p.Category).Distinct().ToList();
            var categoriesText = string.Join(", ", categories.Take(3));

            if (diversityScore > 0.7)
            {
                return $"This diverse route includes {categoriesText} and more, offering a well-rounded experience of the area.";
            }
            else
            {
                return $"This curated route focuses on {categoriesText}, perfectly tailored to your preferences.";
            }
        }

        /// <summary>
        /// Persists the route and suggestions to user's history
        /// </summary>
        private async Task PersistToHistoryAsync(
            Guid userId,
            SurpriseMeResponse response,
            List<Place> selectedPlaces,
            string sessionId,
            CancellationToken cancellationToken)
        {
            // Persist suggestion history (batch)
            var placeTuples = selectedPlaces.Select(p => (p.GooglePlaceId, p.Name, p.Category)).ToList();
            await _userHistoryRepository.AddSuggestionHistoryBatchAsync(
                userId,
                placeTuples,
                "surprise_me",
                sessionId,
                cancellationToken);

            // Persist route history
            var routeDataJson = System.Text.Json.JsonSerializer.Serialize(response.Route);
            await _userHistoryRepository.AddRouteHistoryAsync(
                userId,
                response.Route.Name,
                routeDataJson,
                selectedPlaces.Count,
                response.Route.Id,
                "surprise_me",
                cancellationToken);

            _logger.LogInformation("Persisted Surprise Me route to history for user {UserId}, session {SessionId}",
                userId, sessionId);
        }

        /// <summary>
        /// Calculates budget score based on user preference and place price level
        /// </summary>
        private double CalculateBudgetScore(string budgetLevel, string placePriceLevel)
        {
            if (!int.TryParse(placePriceLevel, out var priceLevel))
                return 0;

            var targetPrice = budgetLevel.ToLower() switch
            {
                "low" => 1,
                "medium" => 2,
                "high" => 3,
                _ => 2
            };

            // Perfect match = 1.0, off by 1 = 0.5, off by 2+ = 0
            var difference = Math.Abs(priceLevel - targetPrice);
            return difference switch
            {
                0 => 1.0,
                1 => 0.5,
                _ => 0.0
            };
        }
    }
}