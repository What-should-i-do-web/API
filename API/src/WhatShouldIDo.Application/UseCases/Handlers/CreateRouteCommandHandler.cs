using MediatR;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Commands;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for creating a new user-defined route
    /// </summary>
    public class CreateRouteCommandHandler : IRequestHandler<CreateRouteCommand, RouteDto>
    {
        private readonly IRouteRepository _routeRepository;
        private readonly IPlacesProvider _placesProvider;
        private readonly ILogger<CreateRouteCommandHandler> _logger;

        public CreateRouteCommandHandler(
            IRouteRepository routeRepository,
            IPlacesProvider placesProvider,
            ILogger<CreateRouteCommandHandler> logger)
        {
            _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
            _placesProvider = placesProvider ?? throw new ArgumentNullException(nameof(placesProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RouteDto> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating route: {RouteName} for user: {UserId}", request.Name, request.UserId);

            // Validate input
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Route name is required", nameof(request.Name));
            }

            if (request.PlaceIds == null || !request.PlaceIds.Any())
            {
                throw new ArgumentException("At least one place is required for a route", nameof(request.PlaceIds));
            }

            // Fetch place details for validation
            var places = new List<PlaceDto>();
            foreach (var placeId in request.PlaceIds)
            {
                try
                {
                    var place = await _placesProvider.GetPlaceDetailsAsync(placeId);
                    if (place != null)
                    {
                        places.Add(place);
                    }
                    else
                    {
                        _logger.LogWarning("Place not found: {PlaceId}", placeId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch place details: {PlaceId}", placeId);
                }
            }

            if (!places.Any())
            {
                throw new InvalidOperationException("No valid places found for the route");
            }

            // Create route entity using domain model
            var route = new Route(request.Name, request.UserId);

            // Set optional properties using domain methods
            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                route.UpdateDescription(request.Description);
            }

            if (request.Tags != null && request.Tags.Any())
            {
                route.SetTags(string.Join(",", request.Tags));
            }

            // Create route points with Coordinates value object
            var routePoints = new List<RoutePoint>();
            int order = 0;
            foreach (var place in places)
            {
                var location = new Domain.ValueObjects.Coordinates(place.Latitude, place.Longitude);
                var routePoint = new RoutePoint(route.Id, location, order);
                route.AddPoint(routePoint);
                routePoints.Add(routePoint);
                order++;
            }

            // Calculate total distance and duration (simplified)
            double totalDistance = 0;
            int totalDuration = 0;
            int defaultStopDuration = 60; // 60 minutes per stop

            for (int i = 0; i < routePoints.Count - 1; i++)
            {
                var current = routePoints[i];
                var next = routePoints[i + 1];

                // Calculate haversine distance using Coordinates value object
                var distance = CalculateDistance(
                    current.Location.Latitude, current.Location.Longitude,
                    next.Location.Latitude, next.Location.Longitude);

                totalDistance += distance;
                totalDuration += defaultStopDuration;

                // Estimate travel time (rough: 5 km/h walking, 30 km/h driving)
                var travelTimeMinutes = request.TransportationMode.ToLower() switch
                {
                    "driving" => (int)(distance / 30.0 * 60),
                    "transit" => (int)(distance / 20.0 * 60),
                    _ => (int)(distance / 5.0 * 60) // walking
                };

                totalDuration += travelTimeMinutes;
            }

            // Add duration for last stop
            if (routePoints.Any())
            {
                totalDuration += defaultStopDuration;
            }

            // Update route with calculated values using domain method
            route.UpdateDistanceAndDuration(totalDistance, totalDuration);

            // Save to repository
            await _routeRepository.AddAsync(route);
            await _routeRepository.SaveChangesAsync();

            _logger.LogInformation("Route created successfully: {RouteId} with {StopCount} stops",
                route.Id, routePoints.Count);

            // Map to DTO
            var routeDto = new RouteDto
            {
                Id = route.Id,
                Name = route.Name,
                Description = route.Description,
                UserId = route.UserId,
                TotalDistance = route.TotalDistance,
                EstimatedDuration = route.EstimatedDuration,
                CreatedAt = route.CreatedAt,
                UpdatedAt = route.UpdatedAt,
                IsPublic = route.IsPublic,
                Tags = request.Tags,
                StopCount = routePoints.Count,
                TransportationMode = request.TransportationMode
            };

            return routeDto;
        }

        /// <summary>
        /// Calculate distance between two points using Haversine formula
        /// Returns distance in kilometers
        /// </summary>
        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth's radius in kilometers

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
