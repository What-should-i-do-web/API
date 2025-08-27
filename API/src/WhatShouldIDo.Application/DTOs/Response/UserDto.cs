using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.DTOs.Response
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}".Trim();
        
        // Subscription info
        public SubscriptionTier SubscriptionTier { get; set; }
        public DateTime? SubscriptionExpiry { get; set; }
        public bool IsSubscriptionActive => SubscriptionExpiry == null || SubscriptionExpiry > DateTime.UtcNow;
        
        // API usage info
        public int DailyApiUsage { get; set; }
        public int DailyApiLimit => SubscriptionTier switch 
        {
            SubscriptionTier.Free => 5,
            SubscriptionTier.Pro => 50, 
            SubscriptionTier.Business => 200,
            _ => 5
        };
        
        // Profile info (if available)
        public UserProfileDto? Profile { get; set; }
        
        // Account info
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
    
    public class UserProfileDto
    {
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Language { get; set; }
        public string? TravelStyle { get; set; }
        public bool IsLocal { get; set; }
        public int? PreferredRadius { get; set; }
        public float PersonalizationScore { get; set; }
    }
    
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
        public UserDto User { get; set; } = null!;
        public string Message { get; set; } = "Login successful";
    }
}