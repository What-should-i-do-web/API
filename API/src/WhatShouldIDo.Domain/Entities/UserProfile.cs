using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Domain.Entities
{
    public class UserProfile
    {
        public Guid Id { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        // Demographic info
        public int? Age { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Language { get; set; } = "en";
        
        // Travel preferences  
        public string? TravelStyle { get; set; } // "luxury", "budget", "backpacker", "family"
        public string? CompanionType { get; set; } // "solo", "couple", "family", "friends"
        public bool IsLocal { get; set; } = true;
        
        // Activity preferences (stored as JSON)
        public string? FavoriteCuisines { get; set; } // ["Turkish", "Italian", "Asian"]
        public string? FavoriteActivityTypes { get; set; } // ["museums", "nightlife", "nature"]
        public string? AvoidedActivityTypes { get; set; } // ["crowded_places", "expensive"]
        
        // Personalization data
        public string? TimePreferences { get; set; } // {"morning": true, "afternoon": true, "evening": false}
        public int? TypicalBudgetPerDay { get; set; } // Daily budget in local currency
        public int? PreferredRadius { get; set; } = 3000; // Default search radius in meters
        
        // Learning from behavior
        public float PersonalizationScore { get; set; } = 0.0f; // How well we know the user (0-1)
        public DateTime LastPreferenceUpdate { get; set; } = DateTime.UtcNow;
        
        // Audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}