using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    public class UserFeedbackRequest
    {
        [Required]
        public Guid PlaceId { get; set; }

        [Required]
        [Range(1, 5)]
        public float Rating { get; set; }

        [MaxLength(1000)]
        public string? Review { get; set; }

        public bool WouldRecommend { get; set; } = true;

        public int? VisitDurationMinutes { get; set; }

        public string? CompanionType { get; set; } // "solo", "couple", "family", "friends"

        public bool ConfirmVisit { get; set; } = false;
    }

    public class VisitConfirmationRequest
    {
        [Required]
        public Guid PlaceId { get; set; }

        public int? DurationMinutes { get; set; }

        public string? Notes { get; set; }
    }
}