using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    public class DayPlanRequest
    {
        [Required]
        public float Latitude { get; set; }

        [Required] 
        public float Longitude { get; set; }

        public string? LocationName { get; set; }

        public int RadiusKm { get; set; } = 10;

        public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0); // 9:00 AM

        public TimeSpan EndTime { get; set; } = new TimeSpan(18, 0, 0); // 6:00 PM

        public List<string> PreferredCategories { get; set; } = new();

        public List<string> AvoidedCategories { get; set; } = new();

        public string? Budget { get; set; } // "low", "medium", "high"

        public string? Transportation { get; set; } // "walking", "driving", "public"

        public bool IncludeMeals { get; set; } = true;

        public string? SpecialRequests { get; set; } // "Family friendly", "Romantic", etc.
    }
}