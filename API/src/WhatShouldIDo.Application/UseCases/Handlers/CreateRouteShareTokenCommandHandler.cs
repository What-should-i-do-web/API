using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Commands;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for creating route share tokens
    /// </summary>
    public class CreateRouteShareTokenCommandHandler : IRequestHandler<CreateRouteShareTokenCommand, RouteShareTokenDto>
    {
        private readonly IRouteRepository _routeRepository;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<CreateRouteShareTokenCommandHandler> _logger;

        public CreateRouteShareTokenCommandHandler(
            IRouteRepository routeRepository,
            IMetricsService metricsService,
            ILogger<CreateRouteShareTokenCommandHandler> logger)
        {
            _routeRepository = routeRepository ?? throw new ArgumentNullException(nameof(routeRepository));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RouteShareTokenDto> Handle(CreateRouteShareTokenCommand command, CancellationToken cancellationToken)
        {
            using var activity = Activity.Current?.Source.StartActivity("Route.CreateShareToken");
            activity?.SetTag("route_id", command.RouteId);
            activity?.SetTag("user_id_hashed", command.UserId.ToString().GetHashCode());

            _logger.LogInformation("Creating share token for route {RouteId} by user {UserId}",
                command.RouteId, command.UserId);

            try
            {
                // Step 1: Verify route exists and user owns it
                var route = await _routeRepository.GetByIdAsync(command.RouteId, cancellationToken);

                if (route == null)
                {
                    _logger.LogWarning("Route {RouteId} not found", command.RouteId);
                    throw new InvalidOperationException($"Route {command.RouteId} not found");
                }

                if (route.UserId != command.UserId)
                {
                    _logger.LogWarning("User {UserId} does not own route {RouteId}", command.UserId, command.RouteId);
                    throw new UnauthorizedAccessException("You can only share routes you own");
                }

                // Step 2: Create share token
                var shareToken = await _routeRepository.CreateShareTokenAsync(
                    command.RouteId,
                    command.UserId,
                    command.ExpiresAt,
                    cancellationToken);

                // Step 3: Record metrics
                _metricsService.IncrementCounter("route_share_tokens_created_total", new[]
                {
                    new KeyValuePair<string, object?>("has_expiry", command.ExpiresAt.HasValue)
                });

                activity?.SetTag("token_created", true);

                _logger.LogInformation("Share token created for route {RouteId}: {Token}",
                    command.RouteId, shareToken.Token);

                // Step 4: Build response DTO
                return new RouteShareTokenDto
                {
                    Id = shareToken.Id,
                    RouteId = shareToken.RouteId,
                    Token = shareToken.Token,
                    ShareUrl = $"/api/routes/shared/{shareToken.Token}",
                    CreatedAt = shareToken.CreatedAt,
                    ExpiresAt = shareToken.ExpiresAt,
                    IsActive = shareToken.IsActive,
                    AccessCount = shareToken.AccessCount,
                    LastAccessedAt = shareToken.LastAccessedAt
                };
            }
            catch (Exception ex) when (ex is not InvalidOperationException && ex is not UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Error creating share token for route {RouteId}", command.RouteId);
                activity?.SetTag("error", true);
                activity?.SetTag("error.message", ex.Message);
                throw;
            }
        }
    }
}
