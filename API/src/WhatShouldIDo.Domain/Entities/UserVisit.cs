using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Domain.Entities
{
    public class UserVisit
    {
        public Guid Id { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required] 
        public Guid PlaceId { get; set; }
        
        [Required]
        public string PlaceName { get; set; } = string.Empty;
        
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        
        // Visit details
        public DateTime VisitDate { get; set; }
        public int? DurationMinutes { get; set; } // How long they stayed
        public string? CompanionType { get; set; } // "solo", "couple", "family", "friends"
        
        // User feedback
        public float? UserRating { get; set; } // 1-5 stars
        public string? UserReview { get; set; }
        public bool WouldRecommend { get; set; } = true;
        public bool WouldVisitAgain { get; set; } = true;
        
        // Context when visit happened
        public string? WeatherCondition { get; set; } // "sunny", "rainy", "cloudy"
        public string? TimeOfDay { get; set; } // "morning", "afternoon", "evening", "night"
        public string? DayOfWeek { get; set; } // "weekday", "weekend"
        
        // Visit source tracking
        public string Source { get; set; } = "app"; // "app", "spontaneous", "friend_recommendation"
        public string? OriginalSuggestionReason { get; set; } // Why we suggested this place
        
        // Learning data
        public bool VisitConfirmed { get; set; } = false; // Did they actually go?
        public DateTime? ConfirmationDate { get; set; }
        
        // Audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Place? Place { get; set; }
    }
}