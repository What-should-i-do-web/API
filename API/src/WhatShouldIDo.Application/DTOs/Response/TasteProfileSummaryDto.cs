using System;
using System.Collections.Generic;

namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// Lightweight taste profile summary for display.
    /// </summary>
    public class TasteProfileSummaryDto
    {
        public Guid UserId { get; set; }
        public string QuizVersion { get; set; } = string.Empty;

        /// <summary>
        /// Interest weights (8 dimensions).
        /// </summary>
        public Dictionary<string, double> Interests { get; set; } = new();

        /// <summary>
        /// Preference weights (5 dimensions).
        /// </summary>
        public Dictionary<string, double> Preferences { get; set; } = new();

        /// <summary>
        /// Novelty tolerance (0=safe, 1=adventurous).
        /// </summary>
        public double NoveltyTolerance { get; set; }

        public DateTime LastUpdatedAt { get; set; }
    }
}
