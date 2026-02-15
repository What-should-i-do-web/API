using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Queries;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for retrieving route revisions
    /// </summary>
    public class GetRouteRevisionsQueryHandler : IRequestHandler<GetRouteRevisionsQuery, List<RouteRevisionDto>>
    {
        private readonly IRouteRepository _routeRepository;
        private readonly ILogger<GetRouteRevisionsQueryHandler> _logger;

        public GetRouteRevisionsQueryHandler(
            IRouteRepository routeRepository,
            ILogger<GetRouteRevisionsQueryHandler> logger)
        {
            _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<RouteRevisionDto>> Handle(GetRouteRevisionsQuery query, CancellationToken cancellationToken)
        {
            using var activity = Activity.Current?.Source.StartActivity("Route.GetRevisions");
            activity?.SetTag("route_id", query.RouteId);

            _logger.LogInformation("Retrieving revisions for route {RouteId}", query.RouteId);

            try
            {
                // Step 1: Verify route exists and user owns it
                var route = await _routeRepository.GetByIdAsync(query.RouteId, cancellationToken);

                if (route == null)
                {
                    _logger.LogWarning("Route {RouteId} not found", query.RouteId);
                    throw new InvalidOperationException($"Route {query.RouteId} not found");
                }

                if (route.UserId != query.UserId)
                {
                    _logger.LogWarning("User {UserId} does not own route {RouteId}", query.UserId, query.RouteId);
                    throw new UnauthorizedAccessException("You can only view revisions of routes you own");
                }

                // Step 2: Get revisions
                var revisions = await _routeRepository.GetRevisionsAsync(query.RouteId, cancellationToken);

                activity?.SetTag("revision_count", revisions.Count());

                // Step 3: Map to DTOs
                var revisionDtos = new List<RouteRevisionDto>();

                foreach (var revision in revisions.OrderByDescending(r => r.RevisionNumber))
                {
                    var dto = new RouteRevisionDto
                    {
                        Id = revision.Id,
                        RouteId = revision.RouteId,
                        RevisionNumber = revision.RevisionNumber,
                        CreatedAt = revision.CreatedAt,
                        Source = revision.Source,
                        ChangeDescription = revision.ChangeDescription
                    };

                    // Optionally deserialize route snapshot
                    try
                    {
                        if (!string.IsNullOrEmpty(revision.RouteDataJson))
                        {
                            var snapshot = JsonSerializer.Deserialize<RouteDto>(revision.RouteDataJson);
                            dto.RouteSnapshot = snapshot;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize route snapshot for revision {RevisionId}", revision.Id);
                    }

                    revisionDtos.Add(dto);
                }

                _logger.LogInformation("Retrieved {Count} revisions for route {RouteId}",
                    revisionDtos.Count, query.RouteId);

                return revisionDtos;
            }
            catch (Exception ex) when (ex is not InvalidOperationException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Error retrieving revisions for route {RouteId}", query.RouteId);
                activity?.SetTag("error", true);
                activity?.SetTag("error.message", ex.Message);
                throw;
            }
        }
    }
}
