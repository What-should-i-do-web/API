using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.Models;
using WhatShouldIDo.Application.Services;
using WhatShouldIDo.Application.UseCases.Commands;
using WhatShouldIDo.Domain.Enums;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for intent-first suggestion orchestration.
    /// Coordinates:
    /// - Policy enforcement (FOOD_ONLY => no non-food, etc.)
    /// - Context building (weather, time, location)
    /// - Provider search (Google/OpenTrip)
    /// - Personalization pipeline (if authenticated)
    /// - Route building (if ROUTE_PLANNING intent)
    /// - Explainability (reasons generation)
    /// </summary>
    public class CreateSuggestionsCommandHandler : IRequestHandler<CreateSuggestionsCommand, SuggestionsResult>
    {
        private readonly IPlacesProvider _placesProvider;
        private readonly ISuggestionPolicy _suggestionPolicy;
        private readonly ISmartSuggestionService _smartSuggestionService;
        private readonly ISuggestionService _suggestionService;
        private readonly IContextEngine _contextEngine;
        private readonly IVariabilityEngine _variabilityEngine;
        private readonly IPreferenceLearningService _preferenceLearningService;
        private readonly IUserHistoryRepository _userHistoryRepository;
        private readonly IRouteOptimizationService _routeOptimizationService;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<CreateSuggestionsCommandHandler> _logger;

        public CreateSuggestionsCommandHandler(
            IPlacesProvider placesProvider,
            ISuggestionPolicy suggestionPolicy,
            ISmartSuggestionService smartSuggestionService,
            ISuggestionService suggestionService,
            IContextEngine contextEngine,
            IVariabilityEngine variabilityEngine,
            IPreferenceLearningService preferenceLearningService,
            IUserHistoryRepository userHistoryRepository,
            IRouteOptimizationService routeOptimizationService,
            IMetricsService metricsService,
            ILogger<CreateSuggestionsCommandHandler> logger)
        {
            _placesProvider = placesProvider ?? throw new ArgumentNullException(nameof(placesProvider));
            _suggestionPolicy = suggestionPolicy ?? throw new ArgumentNullException(nameof(suggestionPolicy));
            _smartSuggestionService = smartSuggestionService ?? throw new ArgumentNullException(nameof(smartSuggestionService));
            _suggestionService = suggestionService ?? throw new ArgumentNullException(nameof(suggestionService));
            _contextEngine = contextEngine ?? throw new ArgumentNullException(nameof(contextEngine));
            _variabilityEngine = variabilityEngine ?? throw new ArgumentNullException(nameof(variabilityEngine));
            _preferenceLearningService = preferenceLearningService ?? throw new ArgumentNullException(nameof(preferenceLearningService));
            _userHistoryRepository = userHistoryRepository ?? throw new ArgumentNullException(nameof(userHistoryRepository));
            _routeOptimizationService = routeOptimizationService ?? throw new ArgumentNullException(nameof(routeOptimizationService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SuggestionsResult> Handle(CreateSuggestionsCommand command, CancellationToken cancellationToken)
        {
            var input = command.Input;
            var stopwatch = Stopwatch.StartNew();

            using var activity = Activity.Current?.Source.StartActivity("Suggestions.Orchestrate");
            activity?.SetTag("intent", input.Intent.ToString());
            activity?.SetTag("authenticated", input.UserId.HasValue);
            activity?.SetTag("radius_meters", input.RadiusMeters);

            _logger.LogInformation("Creating suggestions with intent {Intent} at ({Lat}, {Lng}) radius {Radius}m for user {UserId}",
                input.Intent, input.Latitude, input.Longitude, input.RadiusMeters, input.UserId);

            try
            {
                // Step 1: Validate request against intent policy
                var validationErrors = await _suggestionPolicy.ValidateRequestAsync(
                    input.Intent,
                    (input.Latitude, input.Longitude),
                    input.RadiusMeters,
                    input.WalkingDistanceMeters);

                if (validationErrors.Any())
                {
                    throw new InvalidOperationException($"Intent validation failed: {string.Join(", ", validationErrors)}");
                }

                // Step 2: Get context insights (weather, time, season, location)
                var context = await _contextEngine.GetContextualInsights(
                    (float)input.Latitude,
                    (float)input.Longitude);

                activity?.SetTag("time_of_day", context.TimeContext.ToString());
                activity?.SetTag("season", context.Season.ToString());

                // Step 3: Search for places using provider
                var places = await SearchPlacesAsync(input, context, activity);

                if (!places.Any())
                {
                    _logger.LogWarning("No places found for intent {Intent} at location ({Lat}, {Lng})",
                        input.Intent, input.Latitude, input.Longitude);

                    return CreateEmptyResult(input.Intent, input.UserId);
                }

                _logger.LogInformation("Found {Count} places from provider", places.Count);

                // Step 4: Load user exclusions if authenticated
                var userExclusions = new List<string>();
                if (input.UserId.HasValue)
                {
                    var exclusions = await _userHistoryRepository.GetActiveExclusionsAsync(input.UserId.Value, cancellationToken);
                    userExclusions = exclusions.Select(e => e.PlaceId).ToList();
                }

                // Step 5: Apply intent-based filtering (FOOD_ONLY, ACTIVITY_ONLY constraints)
                places = await _suggestionPolicy.ApplyIntentFilterAsync(
                    input.Intent,
                    places,
                    userExclusions);

                if (!places.Any())
                {
                    _logger.LogWarning("No places remaining after intent filter {Intent}", input.Intent);
                    return CreateEmptyResult(input.Intent, input.UserId);
                }

                _logger.LogInformation("{Count} places after intent filtering", places.Count);

                // Step 6: Apply personalization if authenticated
                List<SuggestionItem> suggestions;
                bool isPersonalized = false;

                if (input.UserId.HasValue)
                {
                    suggestions = await ApplyPersonalizationAsync(
                        input,
                        places,
                        context,
                        activity);
                    isPersonalized = true;
                }
                else
                {
                    // Non-personalized suggestions
                    suggestions = await ConvertToSuggestionsAsync(places, input, context);
                }

                // Step 7: Generate explainability reasons
                suggestions = await EnrichWithReasonsAsync(
                    suggestions,
                    places,
                    input,
                    context,
                    activity);

                // Step 8: Route building if needed
                if (_suggestionPolicy.ShouldBuildRoute(input.Intent))
                {
                    return await BuildRouteResultAsync(
                        input,
                        suggestions,
                        isPersonalized,
                        context,
                        activity);
                }

                // Step 9: Return suggestion list result
                var result = new SuggestionsResult(
                    Intent: input.Intent,
                    IsPersonalized: isPersonalized,
                    UserId: input.UserId,
                    Suggestions: suggestions.Take(input.Intent.GetMaxSuggestions()).ToList(),
                    Route: null,
                    DayPlan: null,
                    TotalCount: suggestions.Count,
                    Filters: new FilterInfo(
                        RadiusMeters: input.RadiusMeters,
                        WalkingDistanceMeters: input.WalkingDistanceMeters,
                        BudgetLevel: input.BudgetLevel,
                        IncludedCategories: input.IncludeCategories?.ToList() ?? new List<string>(),
                        ExcludedCategories: input.ExcludeCategories?.ToList() ?? new List<string>(),
                        DietaryRestrictions: input.DietaryRestrictions?.ToList() ?? new List<string>(),
                        AppliedVariety: input.UserId.HasValue,
                        AppliedContextual: true
                    ),
                    Metadata: new SuggestionMeta(
                        GeneratedAt: DateTime.UtcNow,
                        Source: "IntentOrchestrator",
                        DiversityFactor: _suggestionPolicy.GetDiversityFactor(input.Intent),
                        UsedAI: false,
                        UsedPersonalization: isPersonalized,
                        UsedContextEngine: true,
                        UsedVariabilityEngine: input.UserId.HasValue,
                        TimeOfDay: context.TimeContext.ToString(),
                        WeatherCondition: context.Weather?.Condition,
                        Season: context.Season.ToString()
                    )
                );

                stopwatch.Stop();
                _metricsService.RecordHistogram("suggestion_orchestration_duration_seconds",
                    stopwatch.Elapsed.TotalSeconds,
                    new[]
                    {
                        new KeyValuePair<string, object?>("intent", input.Intent.ToString()),
                        new KeyValuePair<string, object?>("personalized", isPersonalized),
                        new KeyValuePair<string, object?>("route_built", false)
                    });

                activity?.SetTag("suggestions_count", result.TotalCount);
                activity?.SetTag("personalized", isPersonalized);

                _logger.LogInformation("Suggestions orchestration completed in {ElapsedMs}ms: {Count} suggestions, personalized={Personalized}",
                    stopwatch.ElapsedMilliseconds, result.TotalCount, isPersonalized);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in suggestions orchestration for intent {Intent}", input.Intent);
                activity?.SetTag("error", true);
                activity?.SetTag("error.message", ex.Message);

                throw;
            }
        }

        private async Task<List<Place>> SearchPlacesAsync(
            CreateSuggestionsInput input,
            ContextualInsight context,
            Activity? activity)
        {
            using var searchActivity = Activity.Current?.Source.StartActivity("Providers.SearchPlaces");
            searchActivity?.SetTag("provider", "Hybrid");

            var places = await _placesProvider.GetNearbyPlacesAsync(
                (float)input.Latitude,
                (float)input.Longitude,
                input.RadiusMeters);

            searchActivity?.SetTag("places_found", places.Count);

            return places;
        }

        private async Task<List<SuggestionItem>> ApplyPersonalizationAsync(
            CreateSuggestionsInput input,
            List<Place> places,
            ContextualInsight context,
            Activity? activity)
        {
            using var personalizeActivity = Activity.Current?.Source.StartActivity("Personalization.Apply");

            if (input.UserId == null)
            {
                return await ConvertToSuggestionsAsync(places, input, context);
            }

            // Use SmartSuggestionService's personalization pipeline
            var placesForPersonalization = places;

            var personalizedSuggestions = await _smartSuggestionService.ApplyPersonalizationAsync(
                input.UserId.Value,
                placesForPersonalization,
                input.AreaName ?? "nearby");

            personalizeActivity?.SetTag("personalized_count", personalizedSuggestions.Count);

            // Convert DTOs to SuggestionItems
            return personalizedSuggestions.Select(s => new SuggestionItem(
                Id: s.Id,
                PlaceName: s.PlaceName,
                Latitude: s.Latitude,
                Longitude: s.Longitude,
                Category: s.Category,
                Source: s.Source,
                Reason: s.Reason,
                Score: s.Score,
                CreatedAt: s.CreatedAt,
                IsSponsored: s.IsSponsored,
                SponsoredUntil: s.SponsoredUntil,
                PhotoReference: s.PhotoReference,
                PhotoUrl: s.PhotoUrl,
                Reasons: s.ExplainabilityReasons?.Select(r => r.Message).ToList() ?? new List<string>()
            )).ToList();
        }

        private async Task<List<SuggestionItem>> ConvertToSuggestionsAsync(
            List<Place> places,
            CreateSuggestionsInput input,
            ContextualInsight context)
        {
            await Task.CompletedTask;

            return places.Select(p => new SuggestionItem(
                Id: p.Id,
                PlaceName: p.Name,
                Latitude: (float)p.Latitude,
                Longitude: (float)p.Longitude,
                Category: p.Category ?? "",
                Source: p.Source ?? "Unknown",
                Reason: $"Nearby {context.TimeContext.ToString().ToLower()} option",
                Score: string.IsNullOrWhiteSpace(p.Rating) ? 0 : double.TryParse(p.Rating, out var r) ? r : 0,
                CreatedAt: DateTime.UtcNow,
                IsSponsored: p.IsSponsored,
                SponsoredUntil: p.SponsoredUntil,
                PhotoReference: p.PhotoReference,
                PhotoUrl: p.PhotoUrl,
                Reasons: new List<string>()
            )).ToList();
        }

        private async Task<List<SuggestionItem>> EnrichWithReasonsAsync(
            List<SuggestionItem> suggestions,
            List<Place> originalPlaces,
            CreateSuggestionsInput input,
            ContextualInsight context,
            Activity? activity)
        {
            using var reasonActivity = Activity.Current?.Source.StartActivity("Explainability.GenerateReasons");

            var enrichedSuggestions = new List<SuggestionItem>();

            foreach (var suggestion in suggestions)
            {
                var place = originalPlaces.FirstOrDefault(p => p.Id == suggestion.Id);
                if (place == null)
                {
                    enrichedSuggestions.Add(suggestion);
                    continue;
                }

                // Get user preferences if authenticated
                var matchedPreferences = new List<string>();
                if (input.UserId.HasValue)
                {
                    var prefs = await _preferenceLearningService.GetLearnedPreferencesAsync(input.UserId.Value);
                    if (prefs != null)
                    {
                        matchedPreferences = prefs.FavoriteCuisines.Take(2).ToList();
                    }
                }

                // Calculate novelty score
                var noveltyScore = 0.0;
                if (input.UserId.HasValue)
                {
                    noveltyScore = await _variabilityEngine.CalculateNoveltyScoreAsync(input.UserId.Value, place);
                }

                // Get contextual reasons
                var contextualReasons = await _contextEngine.GetContextualReasons(place, context);

                // Generate reasons using policy
                var reasons = await _suggestionPolicy.GenerateReasonsAsync(
                    input.Intent,
                    place,
                    (input.Latitude, input.Longitude),
                    matchedPreferences,
                    noveltyScore,
                    contextualReasons);

                enrichedSuggestions.Add(suggestion with { Reasons = reasons });
            }

            reasonActivity?.SetTag("enriched_count", enrichedSuggestions.Count);

            return enrichedSuggestions;
        }

        private async Task<SuggestionsResult> BuildRouteResultAsync(
            CreateSuggestionsInput input,
            List<SuggestionItem> suggestions,
            bool isPersonalized,
            ContextualInsight context,
            Activity? activity)
        {
            using var routeActivity = Activity.Current?.Source.StartActivity("Route.Build");

            _logger.LogInformation("Building route for ROUTE_PLANNING intent with {Count} suggestions", suggestions.Count);

            // Select diverse places for route (max 8 stops)
            var routePlaces = suggestions.Take(8).ToList();

            // Convert to RouteWaypoints
            var waypoints = routePlaces.Select(s => new RouteWaypoint
            {
                Id = s.Id.ToString(),
                Name = s.PlaceName,
                Latitude = s.Latitude,
                Longitude = s.Longitude
            }).ToList();

            // Optimize order using TSP solver
            var optimizedRoute = await _routeOptimizationService.OptimizeRouteAsync(
                (input.Latitude, input.Longitude),
                waypoints,
                "walking");

            routeActivity?.SetTag("stops_count", routePlaces.Count);
            routeActivity?.SetTag("optimized", optimizedRoute != null);

            // Create RouteResult
            var route = new RouteResult(
                Id: Guid.NewGuid(),
                Name: $"{input.Intent.ToDisplayName()} - {DateTime.UtcNow:yyyy-MM-dd}",
                Description: $"Auto-generated route based on {input.Intent.ToDisplayName()} intent",
                UserId: input.UserId ?? Guid.Empty,
                TotalDistance: optimizedRoute?.TotalDistanceMeters ?? 0,
                EstimatedDuration: optimizedRoute?.TotalDurationSeconds ?? 0,
                StopCount: routePlaces.Count,
                TransportationMode: "walking",
                RouteType: "intent_orchestrated",
                Tags: new[] { input.Intent.ToString().ToLower(), context.TimeContext.ToString().ToLower() },
                IsPublic: false,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow
            );

            return new SuggestionsResult(
                Intent: input.Intent,
                IsPersonalized: isPersonalized,
                UserId: input.UserId,
                Suggestions: new List<SuggestionItem>(),
                Route: route,
                DayPlan: null,
                TotalCount: routePlaces.Count,
                Filters: new FilterInfo(
                    RadiusMeters: input.RadiusMeters,
                    WalkingDistanceMeters: input.WalkingDistanceMeters,
                    BudgetLevel: null,
                    IncludedCategories: new List<string>(),
                    ExcludedCategories: new List<string>(),
                    DietaryRestrictions: new List<string>(),
                    AppliedVariety: true,
                    AppliedContextual: true
                ),
                Metadata: new SuggestionMeta(
                    GeneratedAt: DateTime.UtcNow,
                    Source: "IntentOrchestrator",
                    DiversityFactor: _suggestionPolicy.GetDiversityFactor(input.Intent),
                    UsedAI: false,
                    UsedPersonalization: isPersonalized,
                    UsedContextEngine: true,
                    UsedVariabilityEngine: input.UserId.HasValue,
                    TimeOfDay: context.TimeContext.ToString(),
                    WeatherCondition: context.Weather?.Condition,
                    Season: context.Season.ToString()
                )
            );
        }

        private SuggestionsResult CreateEmptyResult(SuggestionIntent intent, Guid? userId)
        {
            return new SuggestionsResult(
                Intent: intent,
                IsPersonalized: false,
                UserId: userId,
                Suggestions: new List<SuggestionItem>(),
                Route: null,
                DayPlan: null,
                TotalCount: 0,
                Filters: new FilterInfo(
                    RadiusMeters: 0,
                    WalkingDistanceMeters: null,
                    BudgetLevel: null,
                    IncludedCategories: new List<string>(),
                    ExcludedCategories: new List<string>(),
                    DietaryRestrictions: new List<string>(),
                    AppliedVariety: false,
                    AppliedContextual: false
                ),
                Metadata: new SuggestionMeta(
                    GeneratedAt: DateTime.UtcNow,
                    Source: "IntentOrchestrator",
                    DiversityFactor: 0,
                    UsedAI: false,
                    UsedPersonalization: false,
                    UsedContextEngine: false,
                    UsedVariabilityEngine: false,
                    TimeOfDay: null,
                    WeatherCondition: null,
                    Season: null
                )
            );
        }
    }
}
