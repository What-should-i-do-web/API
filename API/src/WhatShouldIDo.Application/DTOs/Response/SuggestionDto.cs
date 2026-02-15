using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Response
{
    public class SuggestionDto
    {
        public Guid Id { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;         // "Geoapify"
        public string Reason { get; set; } = string.Empty;
        public double Score { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserHash { get; set; } = string.Empty;       // (opsiyonel, Random kontrolü için)
        public bool IsSponsored { get; set; } = false;
        public DateTime? SponsoredUntil { get; set; }
        public string? PhotoReference { get; set; }      // Google photo reference
        public string? PhotoUrl { get; set; }            // Generated photo URL

        /// <summary>
        /// Explainability: List of human-readable reasons why this place was suggested.
        /// NEW FIELD (backward compatible - defaults to empty list)
        /// Populated by hybrid scoring system for transparency.
        /// </summary>
        public List<RecommendationReasonDto>? ExplainabilityReasons { get; set; } = null;
    }
}
