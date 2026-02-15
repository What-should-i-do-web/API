using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class DayPlanningService : IDayPlanningService
    {
        private readonly IPlacesProvider _placesProvider;
        private readonly ISmartSuggestionService _smartSuggestionService;
        private readonly IGeocodingService _geocodingService;
        private readonly ILogger<DayPlanningService> _logger;
        private readonly IAIService? _aiService;
        private readonly IPreferenceLearningService? _preferenceLearningService;
        private readonly IMetricsService? _metricsService;
        private readonly AI.DiversityHelper? _diversityHelper;

        public DayPlanningService(
            IPlacesProvider placesProvider,
            ISmartSuggestionService smartSuggestionService,
            IGeocodingService geocodingService,
            ILogger<DayPlanningService> logger,
            IAIService? aiService = null,
            IPreferenceLearningService? preferenceLearningService = null,
            IMetricsService? metricsService = null,
            AI.DiversityHelper? diversityHelper = null)
        {
            _placesProvider = placesProvider;
            _smartSuggestionService = smartSuggestionService;
            _geocodingService = geocodingService;
            _logger = logger;
            _aiService = aiService;
            _preferenceLearningService = preferenceLearningService;
            _metricsService = metricsService;
            _diversityHelper = diversityHelper;
        }

        public async Task<DayPlanDto> CreateDayPlanAsync(DayPlanRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating day plan for location ({Lat}, {Lng})", request.Latitude, request.Longitude);

                // Step 1: Get diverse places by category
                var placesByCategory = await GetPlacesByCategories(request, cancellationToken);

                // Step 2: Create balanced activity sequence
                var activities = CreateBalancedActivitySequence(placesByCategory, request);

                // Step 3: Build the day plan
                var dayPlan = new DayPlanDto
                {
                    Id = Guid.NewGuid(),
                    Title = GeneratePlanTitle(request),
                    Description = GeneratePlanDescription(activities, request),
                    Date = DateTime.Today,
                    EstimatedDuration = request.EndTime - request.StartTime,
                    Budget = request.Budget ?? "medium",
                    Activities = activities,
                    TotalDistance = CalculateTotalDistance(activities),
                    Transportation = request.Transportation ?? "walking",
                    IsPersonalized = false,
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Created day plan with {ActivityCount} activities", activities.Count);
                return dayPlan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating day plan");
                throw;
            }
        }

        public async Task<DayPlanDto> CreatePersonalizedDayPlanAsync(Guid userId, DayPlanRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating personalized day plan for user {UserId}", userId);

                // Step 1: Get personalized suggestions for different categories
                var historicalSuggestions = await GetPersonalizedCategorySuggestions(userId, request, "historical", cancellationToken);
                var restaurantSuggestions = await GetPersonalizedCategorySuggestions(userId, request, "restaurant", cancellationToken);
                var entertainmentSuggestions = await GetPersonalizedCategorySuggestions(userId, request, "entertainment", cancellationToken);

                // Step 2: Create personalized activity sequence
                var activities = CreatePersonalizedActivitySequence(
                    historicalSuggestions,
                    restaurantSuggestions, 
                    entertainmentSuggestions,
                    request);

                // Step 3: Build personalized day plan
                var dayPlan = new DayPlanDto
                {
                    Id = Guid.NewGuid(),
                    Title = GeneratePersonalizedPlanTitle(request),
                    Description = GeneratePersonalizedPlanDescription(activities, request),
                    Date = DateTime.Today,
                    EstimatedDuration = request.EndTime - request.StartTime,
                    Budget = request.Budget ?? "medium",
                    Activities = activities,
                    TotalDistance = CalculateTotalDistance(activities),
                    Transportation = request.Transportation ?? "walking",
                    IsPersonalized = true,
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Created personalized day plan with {ActivityCount} activities for user {UserId}", 
                    activities.Count, userId);
                return dayPlan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating personalized day plan for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<DayPlanDto>> GetSampleDayPlansAsync(float latitude, float longitude, CancellationToken cancellationToken = default)
        {
            try
            {
                var samplePlans = new List<DayPlanDto>();

                // Cultural Explorer Plan
                var culturalRequest = new DayPlanRequest
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    PreferredCategories = new List<string> { "museum", "historical", "cultural" },
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(17, 0, 0),
                    Budget = "medium"
                };
                samplePlans.Add(await CreateDayPlanAsync(culturalRequest, cancellationToken));

                // Food Adventure Plan  
                var foodRequest = new DayPlanRequest
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    PreferredCategories = new List<string> { "restaurant", "cafe", "food" },
                    StartTime = new TimeSpan(11, 0, 0),
                    EndTime = new TimeSpan(20, 0, 0),
                    Budget = "high"
                };
                samplePlans.Add(await CreateDayPlanAsync(foodRequest, cancellationToken));

                // Entertainment & Fun Plan
                var entertainmentRequest = new DayPlanRequest
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    PreferredCategories = new List<string> { "entertainment", "park", "attraction" },
                    StartTime = new TimeSpan(12, 0, 0),
                    EndTime = new TimeSpan(18, 0, 0),
                    Budget = "medium"
                };
                samplePlans.Add(await CreateDayPlanAsync(entertainmentRequest, cancellationToken));

                return samplePlans;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sample day plans");
                return new List<DayPlanDto>();
            }
        }

        private async Task<Dictionary<string, List<Place>>> GetPlacesByCategories(DayPlanRequest request, CancellationToken cancellationToken)
        {
            var placesByCategory = new Dictionary<string, List<Place>>();

            // Define category mappings
            var categoryQueries = new Dictionary<string, string>
            {
                {"historical", "historical sites museums monuments"},
                {"restaurant", "restaurants cafes food"},
                {"entertainment", "entertainment attractions parks activities"}
            };

            foreach (var category in categoryQueries)
            {
                try
                {
                    var places = await _placesProvider.SearchByPromptAsync(
                        category.Value,
                        request.Latitude,
                        request.Longitude,
                        null
                    );

                    // Filter by preferences if specified
                    if (request.PreferredCategories.Any())
                    {
                        places = places.Where(p => 
                            request.PreferredCategories.Any(pref => 
                                p.Category?.ToLower().Contains(pref.ToLower()) == true))
                            .ToList();
                    }

                    // Remove avoided categories
                    if (request.AvoidedCategories.Any())
                    {
                        places = places.Where(p =>
                            !request.AvoidedCategories.Any(avoid =>
                                p.Category?.ToLower().Contains(avoid.ToLower()) == true))
                            .ToList();
                    }

                    placesByCategory[category.Key] = places.Take(5).ToList(); // Limit to top 5 per category
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting places for category {Category}", category.Key);
                    placesByCategory[category.Key] = new List<Place>();
                }
            }

            return placesByCategory;
        }

        private async Task<List<SuggestionDto>> GetPersonalizedCategorySuggestions(
            Guid userId,
            DayPlanRequest request,
            string category,
            CancellationToken cancellationToken)
        {
            try
            {
                var promptRequest = new PromptRequest
                {
                    Prompt = GetCategoryPrompt(category),
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
                };

                var suggestions = await _smartSuggestionService.GetPersonalizedSuggestionsAsync(userId, promptRequest, cancellationToken);
                return suggestions.Take(3).ToList(); // Limit to top 3 per category
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting personalized {Category} suggestions for user {UserId}", category, userId);
                return new List<SuggestionDto>();
            }
        }

        private List<PlannedActivityDto> CreateBalancedActivitySequence(
            Dictionary<string, List<Place>> placesByCategory,
            DayPlanRequest request)
        {
            var activities = new List<PlannedActivityDto>();
            var currentTime = request.StartTime;
            var order = 1;

            // Morning: Historical/Cultural (if available)
            if (placesByCategory.ContainsKey("historical") && placesByCategory["historical"].Any())
            {
                var historicalPlace = placesByCategory["historical"].First();
                activities.Add(CreateActivity(historicalPlace, "historical", order++, currentTime, TimeSpan.FromHours(1.5)));
                currentTime = currentTime.Add(TimeSpan.FromHours(2)); // Including travel time
            }

            // Lunch Break
            if (request.IncludeMeals && placesByCategory.ContainsKey("restaurant") && placesByCategory["restaurant"].Any())
            {
                var restaurant = placesByCategory["restaurant"].First();
                activities.Add(CreateActivity(restaurant, "restaurant", order++, currentTime, TimeSpan.FromHours(1)));
                currentTime = currentTime.Add(TimeSpan.FromHours(1.5)); // Including break time
            }

            // Afternoon: Entertainment/Activities
            if (placesByCategory.ContainsKey("entertainment") && placesByCategory["entertainment"].Any())
            {
                var entertainmentPlace = placesByCategory["entertainment"].First();
                activities.Add(CreateActivity(entertainmentPlace, "entertainment", order++, currentTime, TimeSpan.FromHours(2)));
                currentTime = currentTime.Add(TimeSpan.FromHours(2.5));
            }

            // Add more historical/cultural if time permits
            if (currentTime.Add(TimeSpan.FromHours(1.5)) <= request.EndTime && 
                placesByCategory.ContainsKey("historical") && placesByCategory["historical"].Count > 1)
            {
                var secondHistoricalPlace = placesByCategory["historical"][1];
                activities.Add(CreateActivity(secondHistoricalPlace, "historical", order++, currentTime, TimeSpan.FromHours(1)));
            }

            return activities;
        }

        private List<PlannedActivityDto> CreatePersonalizedActivitySequence(
            List<SuggestionDto> historical,
            List<SuggestionDto> restaurant,
            List<SuggestionDto> entertainment,
            DayPlanRequest request)
        {
            var activities = new List<PlannedActivityDto>();
            var currentTime = request.StartTime;
            var order = 1;

            // Morning: Best historical suggestion
            if (historical.Any())
            {
                var topHistorical = historical.First();
                activities.Add(CreateActivityFromSuggestion(topHistorical, "historical", order++, currentTime, TimeSpan.FromHours(1.5)));
                currentTime = currentTime.Add(TimeSpan.FromHours(2));
            }

            // Lunch: Best restaurant suggestion
            if (request.IncludeMeals && restaurant.Any())
            {
                var topRestaurant = restaurant.First();
                activities.Add(CreateActivityFromSuggestion(topRestaurant, "restaurant", order++, currentTime, TimeSpan.FromHours(1)));
                currentTime = currentTime.Add(TimeSpan.FromHours(1.5));
            }

            // Afternoon: Best entertainment suggestion
            if (entertainment.Any())
            {
                var topEntertainment = entertainment.First();
                activities.Add(CreateActivityFromSuggestion(topEntertainment, "entertainment", order++, currentTime, TimeSpan.FromHours(2)));
                currentTime = currentTime.Add(TimeSpan.FromHours(2.5));
            }

            // Fill remaining time with additional suggestions
            var remainingTime = request.EndTime - currentTime;
            if (remainingTime.TotalHours >= 1)
            {
                var additionalSuggestions = historical.Skip(1).Concat(entertainment.Skip(1)).ToList();
                if (additionalSuggestions.Any())
                {
                    var additional = additionalSuggestions.First();
                    activities.Add(CreateActivityFromSuggestion(additional, "additional", order++, currentTime, TimeSpan.FromHours(1)));
                }
            }

            return activities;
        }

        private PlannedActivityDto CreateActivity(Place place, string activityType, int order, TimeSpan startTime, TimeSpan duration)
        {
            return new PlannedActivityDto
            {
                Order = order,
                ActivityType = activityType,
                PlaceName = place.Name,
                Category = place.Category ?? "",
                Description = "",
                Reason = GetActivityReason(activityType, startTime),
                StartTime = startTime,
                EstimatedDuration = duration,
                Score = double.TryParse(place.Rating, out var rating) ? rating : 3.5,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                PhotoUrl = place.PhotoUrl,
                Address = place.Address,
                Rating = double.TryParse(place.Rating, out var r) ? r : null,
                PriceLevel = place.PriceLevel
            };
        }

        private PlannedActivityDto CreateActivityFromSuggestion(SuggestionDto suggestion, string activityType, int order, TimeSpan startTime, TimeSpan duration)
        {
            return new PlannedActivityDto
            {
                Order = order,
                ActivityType = activityType,
                PlaceName = suggestion.PlaceName,
                Category = suggestion.Category,
                Description = "",
                Reason = suggestion.Reason,
                StartTime = startTime,
                EstimatedDuration = duration,
                Score = suggestion.Score,
                Latitude = suggestion.Latitude,
                Longitude = suggestion.Longitude,
                PhotoUrl = suggestion.PhotoUrl,
                Address = "",
                Rating = suggestion.Score,
                PriceLevel = ""
            };
        }

        private static string GetCategoryPrompt(string category)
        {
            return category switch
            {
                "historical" => "historical sites museums monuments cultural places",
                "restaurant" => "restaurants cafes local food dining",
                "entertainment" => "entertainment attractions parks activities fun",
                _ => category
            };
        }

        private static string GetActivityReason(string activityType, TimeSpan startTime)
        {
            var hour = startTime.Hours;
            return activityType switch
            {
                "historical" when hour < 12 => "Sabah sakinliğinde keşfetmek için ideal",
                "historical" => "Kültürel deneyim için mükemmel",
                "restaurant" when hour < 12 => "Kahvaltı için harika seçenek",
                "restaurant" when hour < 15 => "Öğle molası için uygun",
                "restaurant" => "Akşam yemeği için ideal",
                "entertainment" => "Eğlenceli vakit geçirmek için",
                _ => "Gününüze renk katacak"
            };
        }

        private static string GeneratePlanTitle(DayPlanRequest request)
        {
            if (request.PreferredCategories.Any())
            {
                var mainCategory = request.PreferredCategories.First();
                return $"{mainCategory.ToTitleCase()} Odaklı Gün Planı";
            }

            return request.LocationName != null 
                ? $"{request.LocationName} Keşif Turu"
                : "Özel Gün Planınız";
        }

        private static string GeneratePersonalizedPlanTitle(DayPlanRequest request)
        {
            return request.LocationName != null 
                ? $"{request.LocationName} - Size Özel Plan"
                : "Kişiselleştirilmiş Gün Planınız";
        }

        private static string GeneratePlanDescription(List<PlannedActivityDto> activities, DayPlanRequest request)
        {
            var categories = activities.Select(a => a.ActivityType).Distinct().ToList();
            var categoryText = string.Join(", ", categories.Select(c => c.ToTitleCase()));
            
            return $"{categoryText} içeren {activities.Count} aktiviteli gün planı. " +
                   $"Yaklaşık {(request.EndTime - request.StartTime).TotalHours} saatlik keşif turu.";
        }

        private static string GeneratePersonalizedPlanDescription(List<PlannedActivityDto> activities, DayPlanRequest request)
        {
            return $"Tercihlerinize göre özenle seçilmiş {activities.Count} aktiviteli gün planı. " +
                   $"Her öneri, geçmiş tercihleriniz ve mevcut durumunuz göz önünde bulundurularak hazırlandı.";
        }

        private static float CalculateTotalDistance(List<PlannedActivityDto> activities)
        {
            if (activities.Count < 2) return 0;

            float totalDistance = 0;
            for (int i = 0; i < activities.Count - 1; i++)
            {
                var current = activities[i];
                var next = activities[i + 1];
                totalDistance += CalculateDistance(current.Latitude, current.Longitude, next.Latitude, next.Longitude);
            }

            return totalDistance;
        }

        private static float CalculateDistance(float lat1, float lng1, float lat2, float lng2)
        {
            const double earthRadius = 6371; // Earth's radius in kilometers
            
            var dLat = ToRadians(lat2 - lat1);
            var dLng = ToRadians(lng2 - lng1);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return (float)(earthRadius * c);
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        // ===== AI-Driven Route Generation =====

        public async Task<DayPlanDto> CreateAIDrivenRouteAsync(
            Guid userId,
            DayPlanRequest request,
            double diversityFactor = 0.2,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Check if AI services are available
                if (_aiService == null || _preferenceLearningService == null || _diversityHelper == null)
                {
                    _logger.LogWarning("AI services not available, falling back to standard personalized plan");
                    return await CreatePersonalizedDayPlanAsync(userId, request, cancellationToken);
                }

                _logger.LogInformation("Creating AI-driven route for user {UserId} with diversity factor {Epsilon}",
                    userId, diversityFactor);

                // Step 1: Get user embedding for personalization
                var userEmbedding = await _preferenceLearningService.GetOrUpdateUserEmbeddingAsync(userId, cancellationToken);

                if (userEmbedding == null)
                {
                    _logger.LogWarning("User {UserId} has no embedding, falling back to standard personalized plan", userId);
                    return await CreatePersonalizedDayPlanAsync(userId, request, cancellationToken);
                }

                // Step 2: Get candidate places from all categories
                var allPlaces = await GetCandidatePlacesAsync(request, cancellationToken);

                if (allPlaces.Count == 0)
                {
                    _logger.LogWarning("No places found for location ({Lat}, {Lng})",
                        request.Latitude, request.Longitude);
                    throw new InvalidOperationException("No places found in the specified area");
                }

                _logger.LogInformation("Found {Count} candidate places", allPlaces.Count);

                // Step 3: Generate embeddings for all places and score them
                var scoredPlaces = await ScorePlacesWithEmbeddingsAsync(allPlaces, userEmbedding, cancellationToken);

                // Step 4: Apply ε-greedy selection for diversity
                var activityCount = CalculateActivityCount(request.StartTime, request.EndTime);
                var selectedPlaces = _diversityHelper.EpsilonGreedySelection(
                    scoredPlaces,
                    diversityFactor,
                    activityCount);

                _logger.LogInformation("Selected {Count} places using ε-greedy (ε={Epsilon})",
                    selectedPlaces.Count, diversityFactor);

                // Step 5: Create optimized activities sequence
                var activities = CreateOptimizedActivitySequence(selectedPlaces, request);

                // Step 6: Build the AI-driven day plan
                var dayPlan = new DayPlanDto
                {
                    Id = Guid.NewGuid(),
                    Title = $"AI Personalized Plan for {request.Latitude:F2}, {request.Longitude:F2}",
                    Description = $"AI-curated itinerary with {diversityFactor * 100:F0}% exploration factor",
                    Date = DateTime.Today,
                    EstimatedDuration = request.EndTime - request.StartTime,
                    Budget = request.Budget ?? "medium",
                    Activities = activities,
                    TotalDistance = CalculateTotalDistance(activities),
                    Transportation = request.Transportation ?? "walking",
                    IsPersonalized = true,
                    CreatedAt = DateTime.UtcNow
                };

                // Record metrics
                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                _metricsService?.RecordRouteGenerationDuration(duration);

                _logger.LogInformation("Created AI-driven route with {ActivityCount} activities in {Duration:F2}s",
                    activities.Count, duration);

                return dayPlan;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AI-driven route for user {UserId}", userId);

                // Fallback to standard personalized plan
                _logger.LogInformation("Falling back to standard personalized plan");
                return await CreatePersonalizedDayPlanAsync(userId, request, cancellationToken);
            }
        }

        private async Task<List<Place>> GetCandidatePlacesAsync(
            DayPlanRequest request,
            CancellationToken cancellationToken)
        {
            var allPlaces = new List<Place>();

            // Get places from various categories
            var categoryQueries = new List<string>
            {
                "museums historical sites monuments",
                "restaurants cafes food dining",
                "entertainment attractions parks",
                "shopping markets stores",
                "nightlife bars clubs"
            };

            foreach (var query in categoryQueries)
            {
                try
                {
                    var places = await _placesProvider.SearchByPromptAsync(
                        query,
                        request.Latitude,
                        request.Longitude,
                        null); // priceLevels parameter

                    allPlaces.AddRange(places);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching places for query: {Query}", query);
                }
            }

            // Remove duplicates based on GooglePlaceId
            allPlaces = allPlaces
                .GroupBy(p => p.GooglePlaceId)
                .Select(g => g.First())
                .ToList();

            // Apply user filters
            if (request.PreferredCategories.Any())
            {
                allPlaces = allPlaces.Where(p =>
                    request.PreferredCategories.Any(pref =>
                        p.Category?.Contains(pref, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();
            }

            if (request.AvoidedCategories.Any())
            {
                allPlaces = allPlaces.Where(p =>
                    !request.AvoidedCategories.Any(avoid =>
                        p.Category?.Contains(avoid, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();
            }

            return allPlaces;
        }

        private async Task<List<(Place place, double score)>> ScorePlacesWithEmbeddingsAsync(
            List<Place> places,
            float[] userEmbedding,
            CancellationToken cancellationToken)
        {
            if (_aiService == null)
                throw new InvalidOperationException("AI service not available");

            var scoredPlaces = new List<(Place place, double score)>();

            foreach (var place in places)
            {
                try
                {
                    // Build place description for embedding
                    var placeDescription = $"{place.Name}. {place.Category}. {place.Address}";

                    // Generate place embedding
                    var placeEmbedding = await _aiService.GetEmbeddingAsync(placeDescription, cancellationToken);

                    if (placeEmbedding == null || placeEmbedding.Length == 0)
                    {
                        _logger.LogWarning("Failed to generate embedding for place: {PlaceName}", place.Name);
                        continue;
                    }

                    // Calculate cosine similarity
                    var similarity = AI.DiversityHelper.CosineSimilarity(userEmbedding, placeEmbedding);

                    // Normalize to 0-1 range (cosine similarity is -1 to 1)
                    var score = (similarity + 1.0) / 2.0;

                    scoredPlaces.Add((place, score));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scoring place: {PlaceName}", place.Name);
                }
            }

            return scoredPlaces.OrderByDescending(x => x.score).ToList();
        }

        private List<PlannedActivityDto> CreateOptimizedActivitySequence(
            List<(Place place, double score)> selectedPlaces,
            DayPlanRequest request)
        {
            var activities = new List<PlannedActivityDto>();
            var currentTime = request.StartTime;
            var activityDuration = TimeSpan.FromHours(1.5); // Default 1.5 hours per activity

            for (int i = 0; i < selectedPlaces.Count; i++)
            {
                var (place, score) = selectedPlaces[i];

                // Add buffer time between activities
                if (i > 0)
                {
                    currentTime = currentTime.Add(TimeSpan.FromMinutes(30)); // 30 min travel time
                }

                if (currentTime.Add(activityDuration) > request.EndTime)
                    break;

                activities.Add(new PlannedActivityDto
                {
                    Order = i + 1,
                    ActivityType = place.Category ?? "attraction",
                    PlaceName = place.Name,
                    Category = place.Category ?? "attraction",
                    Description = $"AI-selected based on your preferences",
                    Reason = $"Similarity score: {score:F2}",
                    StartTime = currentTime,
                    EstimatedDuration = activityDuration,
                    Score = score,
                    Latitude = place.Latitude,
                    Longitude = place.Longitude,
                    PhotoUrl = place.PhotoUrl,
                    Address = place.Address
                });

                currentTime = currentTime.Add(activityDuration);
            }

            return activities;
        }

        private static int CalculateActivityCount(TimeSpan startTime, TimeSpan endTime)
        {
            var totalHours = (endTime - startTime).TotalHours;
            // Assume 1.5 hours per activity + 0.5 hours travel time = 2 hours per activity
            return Math.Max(1, (int)(totalHours / 2.0));
        }
    }

    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }
    }
}