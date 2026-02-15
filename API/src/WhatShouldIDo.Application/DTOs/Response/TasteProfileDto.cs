using System;
using System.Collections.Generic;

namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// Full taste profile DTO for API responses.
    /// </summary>
    public class TasteProfileDto
    {
        /// <summary>
        /// Profile ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// User ID this profile belongs to.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Quiz version that generated this profile.
        /// </summary>
        public string QuizVersion { get; set; } = string.Empty;

        /// <summary>
        /// Interest weights (0-1).
        /// </summary>
        public Dictionary<string, double> Interests { get; set; } = new();

        /// <summary>
        /// Preference weights (0-1).
        /// </summary>
        public Dictionary<string, double> Preferences { get; set; } = new();

        /// <summary>
        /// Discovery style (0-1): 0=safe, 0.5=balanced, 1=exploratory.
        /// </summary>
        public double NoveltyTolerance { get; set; }

        /// <summary>
        /// Profile creation time (UTC).
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>
        /// Last profile update time (UTC).
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
