using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Queries;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for retrieving shared routes
    /// </summary>
    public class GetSharedRouteQueryHandler : IRequestHandler<GetSharedRouteQuery, SharedRouteDto?>
    {
        private readonly IRouteRepository _routeRepository;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<GetSharedRouteQueryHandler> _logger;

        public GetSharedRouteQueryHandler(
            IRouteRepository routeRepository,
            IMetricsService metricsService,
            ILogger<GetSharedRouteQueryHandler> logger)
        {
            _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SharedRouteDto?> Handle(GetSharedRouteQuery query, CancellationToken cancellationToken)
        {
            using var activity = Activity.Current?.Source.StartActivity("Route.GetShared");
            activity?.SetTag("token_prefix", query.Token.Length > 4 ? query.Token[..4] + "..." : "***");

            _logger.LogInformation("Retrieving shared route with token");

            try
            {
                // Step 1: Get share token
                var shareToken = await _routeRepository.GetShareTokenAsync(query.Token, cancellationToken);

                if (shareToken == null)
                {
                    _logger.LogWarning("Share token not found");
                    _metricsService.IncrementCounter("route_share_access_total", new[]
                    {
                        new KeyValuePair<string, object?>("status", "not_found")
                    });
                    return null;
                }

                // Step 2: Validate token
                if (!shareToken.IsValid())
                {
                    _logger.LogWarning("Share token is invalid (expired or deactivated)");
                    _metricsService.IncrementCounter("route_share_access_total", new[]
                    {
                        new KeyValuePair<string, object?>("status", "invalid")
                    });

                    // Return minimal info indicating expiration
                    return new SharedRouteDto
                    {
                        IsExpired = true,
                        SharedVia = query.Token
                    };
                }

                // Step 3: Get the route
                var route = await _routeRepository.GetByIdAsync(shareToken.RouteId, cancellationToken);

                if (route == null)
                {
                    _logger.LogWarning("Route {RouteId} for share token no longer exists", shareToken.RouteId);
                    _metricsService.IncrementCounter("route_share_access_total", new[]
                    {
                        new KeyValuePair<string, object?>("status", "route_deleted")
                    });
                    return null;
                }

                // Step 4: Record access
                await _routeRepository.RecordShareAccessAsync(query.Token, cancellationToken);

                _metricsService.IncrementCounter("route_share_access_total", new[]
                {
                    new KeyValuePair<string, object?>("status", "success")
                });

                activity?.SetTag("route_found", true);
                activity?.SetTag("route_id", route.Id);

                // Step 5: Build response DTO (excluding private user data)
                // Note: RoutePoint entity uses Location (Coordinates) with Order only
                return new SharedRouteDto
                {
                    Id = route.Id,
                    Name = route.Name,
                    Description = route.Description,
                    TotalDistance = route.TotalDistance,
                    EstimatedDuration = route.EstimatedDuration,
                    StopCount = route.Points.Count,
                    Tags = route.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries),
                    Points = route.Points.Select(p => new RoutePointDto
                    {
                        Id = p.Id,
                        RouteId = p.RouteId,
                        Order = p.Order,
                        PlaceId = p.Id.ToString(), // Using point ID as place ID
                        PlaceName = $"Stop {p.Order + 1}",
                        Latitude = p.Location.Latitude,
                        Longitude = p.Location.Longitude
                    }).ToList(),
                    CreatedAt = route.CreatedAt,
                    IsExpired = false,
                    SharedVia = query.Token
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving shared route");
                activity?.SetTag("error", true);
                activity?.SetTag("error.message", ex.Message);
                throw;
            }
        }
    }
}
