using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Tracks recently suggested places for exclusion window logic
    /// Implements MRU (Most Recently Used) pattern - keeps last N=20 places
    /// </summary>
    public class UserSuggestionHistory
    {
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string PlaceId { get; set; } = string.Empty;

        public string PlaceName { get; set; } = string.Empty;
        public string? Category { get; set; }

        /// <summary>
        /// When this place was suggested to the user
        /// </summary>
        public DateTime SuggestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Source of suggestion (e.g., "surprise_me", "prompt_search", "nearby")
        /// </summary>
        public string Source { get; set; } = "surprise_me";

        /// <summary>
        /// Sequence number for MRU ordering (higher = more recent)
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Suggestion session ID (groups places suggested together in one request)
        /// </summary>
        public string? SessionId { get; set; }

        // Navigation
        public virtual User User { get; set; } = null!;
    }
}
