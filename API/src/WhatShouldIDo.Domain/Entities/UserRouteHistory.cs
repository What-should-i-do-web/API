using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Tracks user's generated routes for history and re-use
    /// Implements MRU pattern - keeps last N=3 routes
    /// </summary>
    public class UserRouteHistory
    {
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Reference to the actual Route entity (if saved)
        /// </summary>
        public Guid? RouteId { get; set; }

        /// <summary>
        /// Route name/title
        /// </summary>
        public string RouteName { get; set; } = string.Empty;

        /// <summary>
        /// JSON-serialized route data (for quick access without loading full route)
        /// </summary>
        public string RouteDataJson { get; set; } = string.Empty;

        /// <summary>
        /// When this route was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Sequence number for MRU ordering (higher = more recent)
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Source of route generation (e.g., "surprise_me", "manual", "ai_itinerary")
        /// </summary>
        public string Source { get; set; } = "surprise_me";

        /// <summary>
        /// Number of places in the route
        /// </summary>
        public int PlaceCount { get; set; }

        // Navigation
        public virtual User User { get; set; } = null!;
        public virtual Route? Route { get; set; }
    }
}
