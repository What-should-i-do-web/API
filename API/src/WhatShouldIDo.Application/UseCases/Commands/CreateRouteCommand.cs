using MediatR;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.UseCases.Commands
{
    /// <summary>
    /// Command for creating a new user-defined route
    /// </summary>
    public class CreateRouteCommand : IRequest<RouteDto>
    {
        /// <summary>
        /// Route name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// User ID (owner of the route)
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// List of place IDs to include in the route (in order)
        /// </summary>
        public List<string> PlaceIds { get; set; } = new();

        /// <summary>
        /// Whether to auto-optimize the route order based on distance
        /// </summary>
        public bool OptimizeOrder { get; set; } = false;

        /// <summary>
        /// Transportation mode for optimization ("walking", "driving", "transit")
        /// </summary>
        public string TransportationMode { get; set; } = "walking";

        /// <summary>
        /// Route type ("custom", "ai-generated", "recommended")
        /// </summary>
        public string RouteType { get; set; } = "custom";

        /// <summary>
        /// Tags or categories for the route
        /// </summary>
        public List<string> Tags { get; set; } = new();
    }
}
