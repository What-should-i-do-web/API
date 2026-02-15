using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Commands;
using WhatShouldIDo.Domain.ValueObjects;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for rerolling routes with variation
    /// </summary>
    public class RerollRouteCommandHandler : IRequestHandler<RerollRouteCommand, RouteDto>
    {
        private readonly IRouteRepository _routeRepository;
        private readonly IPlacesProvider _placesProvider;
        private readonly IVariabilityEngine _variabilityEngine;
        private readonly IRouteOptimizationService _routeOptimizationService;
        private readonly ISmartSuggestionService _smartSuggestionService;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<RerollRouteCommandHandler> _logger;

        public RerollRouteCommandHandler(
            IRouteRepository routeRepository,
            IPlacesProvider placesProvider,
            IVariabilityEngine variabilityEngine,
            IRouteOptimizationService routeOptimizationService,
            ISmartSuggestionService smartSuggestionService,
            IMetricsService metricsService,
            ILogger<RerollRouteCommandHandler> logger)
        {
            _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
            _placesProvider = placesProvider ?? throw new ArgumentNullException(nameof(placesProvider));
            _variabilityEngine = variabilityEngine ?? throw new ArgumentNullException(nameof(variabilityEngine));
            _routeOptimizationService = routeOptimizationService ?? throw new ArgumentNullException(nameof(routeOptimizationService));
            _smartSuggestionService = smartSuggestionService ?? throw new ArgumentNullException(nameof(smartSuggestionService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RouteDto> Handle(RerollRouteCommand command, CancellationToken cancellationToken)
        {
            using var activity = Activity.Current?.Source.StartActivity("Route.Reroll");
            activity?.SetTag("route_id", command.RouteId);
            activity?.SetTag("user_id_hashed", command.UserId.ToString().GetHashCode());
            activity?.SetTag("variation_factor", command.VariationFactor);

            _logger.LogInformation("Rerolling route {RouteId} for user {UserId} with variation {Variation}",
                command.RouteId, command.UserId, command.VariationFactor);

            try
            {
                // Step 1: Get existing route
                var route = await _routeRepository.GetByIdAsync(command.RouteId, cancellationToken);

                if (route == null)
                {
                    _logger.LogWarning("Route {RouteId} not found", command.RouteId);
                    throw new InvalidOperationException($"Route {command.RouteId} not found");
                }

                if (route.UserId != command.UserId)
                {
                    _logger.LogWarning("User {UserId} does not own route {RouteId}", command.UserId, command.RouteId);
                    throw new UnauthorizedAccessException("You can only reroll routes you own");
                }

                // Step 2: Save revision before reroll if requested
                if (command.SaveRevisionBeforeReroll)
                {
                    var routeSnapshot = SerializeRouteToJson(route);
                    await _routeRepository.CreateRevisionAsync(
                        route.Id,
                        routeSnapshot,
                        command.UserId,
                        "pre_reroll",
                        "Auto-saved before reroll",
                        cancellationToken);

                    _logger.LogDebug("Created revision for route {RouteId} before reroll", command.RouteId);
                }

                // Step 3: Extract route constraints from existing route
                var existingPoints = route.Points.ToList();
                if (!existingPoints.Any())
                {
                    throw new InvalidOperationException("Cannot reroll a route with no points");
                }

                // Calculate center point and radius from existing route
                var centerLat = existingPoints.Average(p => p.Location.Latitude);
                var centerLng = existingPoints.Average(p => p.Location.Longitude);
                var maxDistance = existingPoints.Max(p =>
                    HaversineDistance(centerLat, centerLng, p.Location.Latitude, p.Location.Longitude));
                var radiusMeters = (int)Math.Max(maxDistance * 1.5, 1000);

                // Step 4: Search for new places
                var newPlaces = await _placesProvider.GetNearbyPlacesAsync(
                    (float)centerLat,
                    (float)centerLng,
                    radiusMeters);

                if (!newPlaces.Any())
                {
                    _logger.LogWarning("No places found for reroll at ({Lat}, {Lng})", centerLat, centerLng);
                    throw new InvalidOperationException("No alternative places found in this area");
                }

                // Step 5: Apply variability to select different places
                var existingPointIds = existingPoints.Select(p => p.Id).ToHashSet();
                var candidatePlaces = newPlaces
                    .Where(p => command.VariationFactor > 0.5 || !existingPointIds.Contains(p.Id))
                    .ToList();

                var desiredStopCount = command.KeepStopCount
                    ? existingPoints.Count
                    : Math.Max(existingPoints.Count, 5);

                var variedPlaces = await _variabilityEngine.FilterForVarietyAsync(
                    command.UserId,
                    candidatePlaces,
                    cancellationToken);

                // Step 6: Optimize new route
                var targetStopCount = command.KeepStopCount
                    ? existingPoints.Count
                    : variedPlaces.Count;

                var selectedPlaces = variedPlaces
                    .Take(targetStopCount)
                    .ToList();


                // Convert to waypoints
                var waypoints = selectedPlaces.Select(p => new RouteWaypoint
                {
                    Id = p.Id.ToString(),
                    Name = p.Name,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude
                }).ToList();

                var optimizedRoute = await _routeOptimizationService.OptimizeRouteAsync(
                    (centerLat, centerLng),
                    waypoints,
                    "walking",
                    cancellationToken);

                // Step 7: Update route with new points
                // Clear existing points and add new ones
                var updatedRoute = new Domain.Entities.Route(route.Name + " (Rerolled)", route.UserId);
                updatedRoute.UpdateDescription($"Rerolled version of original route with {command.VariationFactor:P0} variation");

                int order = 0;
                foreach (var place in selectedPlaces)
                {
                    var coordinates = new Coordinates(place.Latitude, place.Longitude);
                    var point = new Domain.Entities.RoutePoint(updatedRoute.Id, coordinates, order++);
                    updatedRoute.AddPoint(point);
                }

                updatedRoute.UpdateDistanceAndDuration(
                    optimizedRoute?.TotalDistanceMeters ?? 0,
                    optimizedRoute?.TotalDurationSeconds ?? 0);

                updatedRoute.SetTags(route.Tags);

                // Step 8: Save new route
                var savedRoute = await _routeRepository.AddAsync(updatedRoute, cancellationToken);
                await _routeRepository.SaveChangesAsync(cancellationToken);

                // Step 9: Create revision for new route
                var newRouteSnapshot = SerializeRouteToJson(savedRoute);
                await _routeRepository.CreateRevisionAsync(
                    savedRoute.Id,
                    newRouteSnapshot,
                    command.UserId,
                    "reroll",
                    $"Rerolled from route {route.Id}",
                    cancellationToken);

                // Step 10: Record metrics
                _metricsService.IncrementCounter("route_reroll_total", new[]
                {
                    new KeyValuePair<string, object?>("variation_factor", command.VariationFactor > 0.5 ? "high" : "low"),
                    new KeyValuePair<string, object?>("stop_count_changed", !command.KeepStopCount)
                });

                activity?.SetTag("new_route_id", savedRoute.Id);
                activity?.SetTag("stops_count", savedRoute.Points.Count);

                _logger.LogInformation("Route rerolled successfully. Original: {OriginalId}, New: {NewId}",
                    command.RouteId, savedRoute.Id);

                // Step 11: Build response DTO
                return new RouteDto
                {
                    Id = savedRoute.Id,
                    Name = savedRoute.Name,
                    Description = savedRoute.Description,
                    UserId = savedRoute.UserId,
                    TotalDistance = savedRoute.TotalDistance,
                    EstimatedDuration = savedRoute.EstimatedDuration,
                    StopCount = savedRoute.Points.Count,
                    TransportationMode = "walking",
                    RouteType = "rerolled",
                    Tags = savedRoute.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    IsPublic = savedRoute.IsPublic,
                    CreatedAt = savedRoute.CreatedAt,
                    UpdatedAt = savedRoute.UpdatedAt
                };
            }
            catch (Exception ex) when (ex is not InvalidOperationException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Error rerolling route {RouteId}", command.RouteId);
                activity?.SetTag("error", true);
                activity?.SetTag("error.message", ex.Message);
                throw;
            }
        }

        private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusMeters = 6371000;

            var lat1Rad = lat1 * Math.PI / 180.0;
            var lat2Rad = lat2 * Math.PI / 180.0;
            var deltaLat = (lat2 - lat1) * Math.PI / 180.0;
            var deltaLon = (lon2 - lon1) * Math.PI / 180.0;

            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusMeters * c;
        }

        private static string SerializeRouteToJson(Domain.Entities.Route route)
        {
            var snapshot = new
            {
                route.Id,
                route.Name,
                route.Description,
                route.TotalDistance,
                route.EstimatedDuration,
                route.Tags,
                Points = route.Points.Select(p => new
                {
                    p.Id,
                    p.RouteId,
                    p.Order,
                    Latitude = p.Location.Latitude,
                    Longitude = p.Location.Longitude
                }).ToList()
            };

            return JsonSerializer.Serialize(snapshot);
        }
    }
}
