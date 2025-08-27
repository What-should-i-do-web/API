using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    public class UpdateProfileRequest
    {
        [MaxLength(100)]
        public string? FirstName { get; set; }
        
        [MaxLength(100)]
        public string? LastName { get; set; }
        
        [MaxLength(100)]
        public string? City { get; set; }
        
        [MaxLength(100)]
        public string? Country { get; set; }
        
        [MaxLength(10)]
        public string? Language { get; set; }
        
        public int? Age { get; set; }
        
        [MaxLength(50)]
        public string? TravelStyle { get; set; } // "luxury", "budget", "backpacker", "family"
        
        [MaxLength(50)]
        public string? CompanionType { get; set; } // "solo", "couple", "family", "friends"
        
        public bool? IsLocal { get; set; }
        
        public int? PreferredRadius { get; set; }
        
        public int? TypicalBudgetPerDay { get; set; }
        
        // JSON arrays as strings for flexibility
        public string? PreferredCuisines { get; set; } // ["Turkish", "Italian"]
        public string? ActivityPreferences { get; set; } // ["museums", "outdoors"]
        public string? FavoriteCuisines { get; set; }
        public string? FavoriteActivityTypes { get; set; }
        public string? AvoidedActivityTypes { get; set; }
        public string? TimePreferences { get; set; } // {"morning": true, "afternoon": true}
        
        [MaxLength(20)]
        public string? BudgetRange { get; set; } // "low", "medium", "high"
        
        [MaxLength(20)]
        public string? MobilityLevel { get; set; } // "high", "medium", "low"
    }
}