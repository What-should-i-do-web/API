using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Tracks user interactions with places for preference learning
    /// </summary>
    public class UserAction
    {
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string PlaceId { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? PlaceName { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>
        /// Action type: "view", "favorite", "visit", "rate", "share", "reject"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string ActionType { get; set; } = "view";

        /// <summary>
        /// User rating if applicable (1.0 - 5.0)
        /// </summary>
        public float? Rating { get; set; }

        /// <summary>
        /// Time spent on place details page in seconds (for "view" actions)
        /// </summary>
        public int? DurationSeconds { get; set; }

        /// <summary>
        /// Additional metadata as JSON (e.g., search context, filters applied)
        /// </summary>
        public string? Metadata { get; set; }

        public DateTime ActionTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this action has been processed for preference learning
        /// </summary>
        public bool IsProcessed { get; set; } = false;

        public DateTime? ProcessedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
