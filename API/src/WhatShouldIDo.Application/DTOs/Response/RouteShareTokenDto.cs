namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// DTO for route share token
    /// </summary>
    public class RouteShareTokenDto
    {
        /// <summary>
        /// The share token ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The route ID
        /// </summary>
        public Guid RouteId { get; set; }

        /// <summary>
        /// The share token string (used in URLs)
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Full share URL
        /// </summary>
        public string ShareUrl { get; set; } = string.Empty;

        /// <summary>
        /// When the token was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the token expires (null = never)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Whether the token is currently active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Number of times the shared route has been accessed
        /// </summary>
        public int AccessCount { get; set; }

        /// <summary>
        /// Last time the shared route was accessed
        /// </summary>
        public DateTime? LastAccessedAt { get; set; }
    }

    /// <summary>
    /// DTO for shared route (read-only view, no private user data)
    /// </summary>
    public class SharedRouteDto
    {
        /// <summary>
        /// Route ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Route name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Route description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Total distance in meters
        /// </summary>
        public double TotalDistance { get; set; }

        /// <summary>
        /// Estimated duration in minutes
        /// </summary>
        public int EstimatedDuration { get; set; }

        /// <summary>
        /// Number of stops
        /// </summary>
        public int StopCount { get; set; }

        /// <summary>
        /// Route tags
        /// </summary>
        public string[]? Tags { get; set; }

        /// <summary>
        /// Route points (locations)
        /// </summary>
        public List<RoutePointDto> Points { get; set; } = new();

        /// <summary>
        /// When the route was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Whether this shared view has expired
        /// </summary>
        public bool IsExpired { get; set; }

        /// <summary>
        /// Share token used to access this route
        /// </summary>
        public string SharedVia { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for route revision
    /// </summary>
    public class RouteRevisionDto
    {
        /// <summary>
        /// Revision ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Route ID
        /// </summary>
        public Guid RouteId { get; set; }

        /// <summary>
        /// Revision number (1, 2, 3, etc.)
        /// </summary>
        public int RevisionNumber { get; set; }

        /// <summary>
        /// When this revision was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Source of the revision (manual_edit, reroll, ai_generated)
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Description of what changed
        /// </summary>
        public string? ChangeDescription { get; set; }

        /// <summary>
        /// Snapshot of the route at this revision (deserialized)
        /// </summary>
        public RouteDto? RouteSnapshot { get; set; }
    }
}
