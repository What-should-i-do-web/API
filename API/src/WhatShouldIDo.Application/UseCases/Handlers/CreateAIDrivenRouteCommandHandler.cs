using MediatR;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Commands;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for creating AI-driven personalized routes
    /// Uses user preference embeddings and diversity algorithms
    /// </summary>
    public class CreateAIDrivenRouteCommandHandler : IRequestHandler<CreateAIDrivenRouteCommand, DayPlanDto>
    {
        private readonly IDayPlanningService _dayPlanningService;
        private readonly IPreferenceLearningService _preferenceLearningService;
        private readonly ILogger<CreateAIDrivenRouteCommandHandler> _logger;

        public CreateAIDrivenRouteCommandHandler(
            IDayPlanningService dayPlanningService,
            IPreferenceLearningService preferenceLearningService,
            ILogger<CreateAIDrivenRouteCommandHandler> logger)
        {
            _dayPlanningService = dayPlanningService ?? throw new ArgumentNullException(nameof(dayPlanningService));
            _preferenceLearningService = preferenceLearningService ?? throw new ArgumentNullException(nameof(preferenceLearningService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DayPlanDto> Handle(CreateAIDrivenRouteCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating AI-driven route for user {UserId} at ({Lat}, {Lng}) with diversity factor {Epsilon}",
                request.UserId, request.Latitude, request.Longitude, request.DiversityFactor);

            try
            {
                // Validate diversity factor
                if (request.DiversityFactor < 0.0 || request.DiversityFactor > 1.0)
                {
                    throw new ArgumentException("Diversity factor must be between 0.0 and 1.0", nameof(request.DiversityFactor));
                }

                // Check if user has sufficient data for AI-driven routing
                var personalizationScore = await _preferenceLearningService.CalculatePersonalizationScoreAsync(
                    request.UserId,
                    cancellationToken);

                if (personalizationScore < 0.1f)
                {
                    _logger.LogWarning("User {UserId} has low personalization score ({Score}), may get suboptimal results",
                        request.UserId, personalizationScore);
                }

                // Map command to DayPlanRequest
                var dayPlanRequest = new DayPlanRequest
                {
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    LocationName = request.LocationName,
                    RadiusKm = request.RadiusKm,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    PreferredCategories = request.PreferredCategories,
                    AvoidedCategories = request.AvoidedCategories,
                    Budget = request.Budget,
                    Transportation = request.Transportation,
                    IncludeMeals = request.IncludeMeals,
                    SpecialRequests = request.SpecialRequests
                };

                // Create AI-driven route using the service
                var dayPlan = await _dayPlanningService.CreateAIDrivenRouteAsync(
                    request.UserId,
                    dayPlanRequest,
                    request.DiversityFactor,
                    cancellationToken);

                _logger.LogInformation("Successfully created AI-driven route with {ActivityCount} activities for user {UserId}",
                    dayPlan.Activities.Count, request.UserId);

                return dayPlan;
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid AI-driven route creation request for user {UserId}", request.UserId);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot create AI-driven route for user {UserId}: {Message}",
                    request.UserId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AI-driven route for user {UserId}", request.UserId);
                throw new InvalidOperationException($"Failed to create AI-driven route: {ex.Message}", ex);
            }
        }
    }
}
