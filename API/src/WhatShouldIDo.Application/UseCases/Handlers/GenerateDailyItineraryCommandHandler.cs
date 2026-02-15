using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.AI;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Commands;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for generating AI-driven daily itineraries with personalization
    /// </summary>
    public class GenerateDailyItineraryCommandHandler : IRequestHandler<GenerateDailyItineraryCommand, AIItinerary>
    {
        private readonly IAIService _aiService;
        private readonly IPlacesProvider _placesProvider;
        private readonly IRouteService _routeService;
        private readonly IPreferenceLearningService _preferenceLearningService;
        private readonly ILogger<GenerateDailyItineraryCommandHandler> _logger;

        public GenerateDailyItineraryCommandHandler(
            IAIService aiService,
            IPlacesProvider placesProvider,
            IRouteService routeService,
            IPreferenceLearningService preferenceLearningService,
            ILogger<GenerateDailyItineraryCommandHandler> logger)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _placesProvider = placesProvider ?? throw new ArgumentNullException(nameof(placesProvider));
            _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
            _preferenceLearningService = preferenceLearningService ?? throw new ArgumentNullException(nameof(preferenceLearningService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AIItinerary> Handle(GenerateDailyItineraryCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Generating daily itinerary for user {UserId} at location {Location}",
                request.UserId, request.Location);

            try
            {
                // Step 1: Build AI request from command
                var aiRequest = new AIItineraryRequest
                {
                    UserId = request.UserId,
                    Location = request.Location,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    TargetDate = request.TargetDate ?? DateTime.Today,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    PreferredActivities = request.PreferredActivities,
                    DietaryPreferences = request.DietaryPreferences,
                    BudgetLevel = request.BudgetLevel,
                    RadiusMeters = request.RadiusMeters,
                    MaxStops = request.MaxStops,
                    TransportationMode = request.TransportationMode,
                    AdditionalPreferences = request.AdditionalPreferences
                };

                // Step 2: Enhance with user preferences if user is authenticated
                if (request.UserId.HasValue)
                {
                    try
                    {
                        var preferences = await _preferenceLearningService.GetLearnedPreferencesAsync(
                            request.UserId.Value,
                            cancellationToken);

                        // Merge learned preferences with explicit preferences
                        if (preferences != null)
                        {
                            if (!aiRequest.PreferredActivities.Any() && preferences.FavoriteCategories.Any())
                            {
                                aiRequest.PreferredActivities = preferences.FavoriteCategories.ToList();
                                _logger.LogDebug("Applied learned category preferences for user {UserId}", request.UserId);
                            }

                            if (!aiRequest.DietaryPreferences.Any() && preferences.DietaryRestrictions.Any())
                            {
                                aiRequest.DietaryPreferences = preferences.DietaryRestrictions.ToList();
                                _logger.LogDebug("Applied learned dietary preferences for user {UserId}", request.UserId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load user preferences, continuing without personalization");
                    }
                }

                // Step 3: Generate AI-driven itinerary
                var itinerary = await _aiService.GenerateDailyItineraryAsync(aiRequest, cancellationToken);

                if (itinerary == null || !itinerary.Stops.Any())
                {
                    _logger.LogWarning("AI generated empty itinerary for {Location}", request.Location);
                    throw new InvalidOperationException($"Failed to generate itinerary for {request.Location}");
                }

                _logger.LogInformation("AI generated itinerary with {StopCount} stops: {Title}",
                    itinerary.Stops.Count, itinerary.Title);

                // Step 4: Optionally save as a route if requested
                if (request.SaveAsRoute && request.UserId.HasValue)
                {
                    try
                    {
                        // Create route using the service
                        var createRouteRequest = new CreateRouteRequest
                        {
                            Name = itinerary.Title,
                            Description = itinerary.Description
                        };

                        var savedRoute = await _routeService.CreateAsync(createRouteRequest);

                        _logger.LogInformation("Saved itinerary as route {RouteId} for user {UserId}",
                            savedRoute.Id, request.UserId);

                        // Add route ID to itinerary metadata
                        if (itinerary.Stops.Any())
                        {
                            var firstStop = itinerary.Stops.First();
                            if (firstStop.Place.Metadata == null)
                            {
                                firstStop.Place.Metadata = new Dictionary<string, object>();
                            }
                            firstStop.Place.Metadata["savedRouteId"] = savedRoute.Id.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save itinerary as route, continuing anyway");
                        // Don't fail the entire operation if saving fails
                    }
                }

                // Step 5: Track user action for future personalization
                if (request.UserId.HasValue)
                {
                    try
                    {
                        foreach (var stop in itinerary.Stops)
                        {
                            if (stop.Place.Types != null && stop.Place.Types.Any())
                            {
                                await _preferenceLearningService.TrackUserActionAsync(
                                    request.UserId.Value,
                                    stop.Place.PlaceId,
                                    "itinerary_generated",
                                    placeName: stop.Place.Name,
                                    category: stop.Place.Types.FirstOrDefault() ?? "unknown",
                                    cancellationToken: cancellationToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to track user actions for personalization");
                    }
                }

                return itinerary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate daily itinerary for {Location}", request.Location);
                throw;
            }
        }
    }
}
