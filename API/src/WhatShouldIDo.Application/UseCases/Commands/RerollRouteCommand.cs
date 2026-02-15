using MediatR;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.UseCases.Commands
{
    /// <summary>
    /// Command to regenerate a route with variation while keeping similar constraints
    /// </summary>
    public class RerollRouteCommand : IRequest<RouteDto>
    {
        /// <summary>
        /// The route to reroll
        /// </summary>
        public Guid RouteId { get; set; }

        /// <summary>
        /// User requesting the reroll (must be route owner)
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// How much variation to introduce (0.0 = minimal, 1.0 = maximum)
        /// </summary>
        public double VariationFactor { get; set; } = 0.5;

        /// <summary>
        /// Whether to keep the same number of stops
        /// </summary>
        public bool KeepStopCount { get; set; } = true;

        /// <summary>
        /// Whether to save the current route as a revision before rerolling
        /// </summary>
        public bool SaveRevisionBeforeReroll { get; set; } = true;
    }
}
