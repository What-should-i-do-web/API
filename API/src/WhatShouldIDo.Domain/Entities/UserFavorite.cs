using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Tracks user's favorite places for personalization and quick access
    /// </summary>
    public class UserFavorite
    {
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string PlaceId { get; set; } = string.Empty;

        public string PlaceName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        /// <summary>
        /// When the user added this favorite
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional note from user about why they favorited this
        /// </summary>
        public string? Notes { get; set; }

        // Navigation
        public virtual User User { get; set; } = null!;
    }
}
