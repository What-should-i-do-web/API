using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string UserName { get; set; } = string.Empty;
        
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        
        public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
        public DateTime? SubscriptionExpiry { get; set; }
        
        // User preferences
        public string? PreferredCuisines { get; set; } // JSON array: ["Turkish", "Italian"] 
        public string? ActivityPreferences { get; set; } // JSON array: ["museums", "outdoors"]
        public string? BudgetRange { get; set; } // "low", "medium", "high"
        public string? MobilityLevel { get; set; } // "high", "medium", "low"
        
        // API usage tracking
        public int DailyApiUsage { get; set; } = 0;
        public DateTime LastApiReset { get; set; } = DateTime.UtcNow;
        
        // Audit fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual ICollection<UserVisit> VisitHistory { get; set; } = new List<UserVisit>();
        public virtual UserProfile? Profile { get; set; }
    }
    
    public enum SubscriptionTier
    {
        Free = 0,
        Pro = 1, 
        Business = 2
    }
}