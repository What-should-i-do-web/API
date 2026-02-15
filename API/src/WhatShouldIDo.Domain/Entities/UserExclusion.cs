using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Tracks places the user explicitly marked as "do not recommend"
    /// Hard exclusion with optional TTL
    /// </summary>
    public class UserExclusion
    {
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string PlaceId { get; set; } = string.Empty;

        public string PlaceName { get; set; } = string.Empty;

        /// <summary>
        /// When this exclusion was added
        /// </summary>
        public DateTime ExcludedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this exclusion expires (null = never expires)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Optional reason for exclusion (e.g., "bad experience", "not interested")
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// True if this is a permanent exclusion
        /// </summary>
        public bool IsPermanent => ExpiresAt == null;

        // Navigation
        public virtual User User { get; set; } = null!;
    }
}
