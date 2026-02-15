namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request for creating a new route
    /// </summary>
    public class CreateRouteRequest
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
        /// List of place IDs to include in the route (in order)
        /// </summary>
        public List<string>? PlaceIds { get; set; }

        /// <summary>
        /// Whether to auto-optimize the route order based on distance
        /// </summary>
        public bool OptimizeOrder { get; set; } = false;

        /// <summary>
        /// Transportation mode for optimization ("walking", "driving", "transit")
        /// </summary>
        public string? TransportationMode { get; set; }

        /// <summary>
        /// Tags or categories for the route
        /// </summary>
        public List<string>? Tags { get; set; }
    }
}
