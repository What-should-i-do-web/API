namespace WhatShouldIDo.Application.DTOs.Request;

public class FilterCriteria
{
    // Location filters
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? Radius { get; set; } = 5000; // meters
    
    // Category filters
    public List<string>? Categories { get; set; }
    public List<string>? ExcludeCategories { get; set; }
    
    // Rating & Score filters
    public double? MinRating { get; set; }
    public double? MaxRating { get; set; }
    public double? MinScore { get; set; }
    
    // Time-based filters
    public TimeOfDay? TimeOfDay { get; set; }
    public List<DayOfWeek>? PreferredDays { get; set; }
    public bool? OpenNow { get; set; }
    
    // Weather-based filters
    public WeatherCondition? WeatherCondition { get; set; }
    public bool? IndoorOnly { get; set; }
    public bool? OutdoorOnly { get; set; }
    
    // Budget filters
    public PriceLevel? MaxPriceLevel { get; set; }
    public bool? FreeOnly { get; set; }
    
    // Accessibility filters
    public bool? WheelchairAccessible { get; set; }
    public bool? PetFriendly { get; set; }
    public bool? FamilyFriendly { get; set; }
    
    // Social filters
    public bool? PopularWithLocals { get; set; }
    public bool? TrendingNow { get; set; }
    public int? MinReviewCount { get; set; }
    
    // Personalization
    public bool? MatchPreferences { get; set; }
    public string? UserHash { get; set; }
    
    // Result controls
    public int? Limit { get; set; } = 20;
    public SortBy? SortBy { get; set; } = Request.SortBy.Relevance;
    public bool? IncludeSponsored { get; set; } = true;
    
    // Advanced filters
    public List<string>? Keywords { get; set; }
    public bool? HasPhotos { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public List<string>? Sources { get; set; } // "Google", "OpenTripMap", etc.
}

public enum TimeOfDay
{
    EarlyMorning,   // 6-9 AM
    Morning,        // 9-12 PM  
    Afternoon,      // 12-5 PM
    Evening,        // 5-8 PM
    Night,          // 8 PM-12 AM
    LateNight       // 12-6 AM
}

public enum WeatherCondition
{
    Sunny,
    Rainy,
    Cloudy,
    Snowy,
    Windy,
    Hot,
    Cold
}

public enum PriceLevel
{
    Free = 0,
    Inexpensive = 1,
    Moderate = 2,
    Expensive = 3,
    VeryExpensive = 4
}

public enum SortBy
{
    Relevance,
    Distance,
    Rating,
    Score,
    Popularity,
    Recent,
    Name,
    PriceAscending,
    PriceDescending
}