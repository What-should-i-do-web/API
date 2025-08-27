namespace WhatShouldIDo.Application.DTOs.Response;

public class SystemHealthDto
{
    public string Status { get; set; } = "healthy";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Services { get; set; } = new();
    public Dictionary<string, object> Performance { get; set; } = new();
    public Dictionary<string, object> Resources { get; set; } = new();
}

public class UsageAnalyticsDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public long TotalRequests { get; set; }
    public long UniqueUsers { get; set; }
    public Dictionary<string, long> EndpointUsage { get; set; } = new();
    public Dictionary<string, long> CategoryPopularity { get; set; } = new();
    public Dictionary<string, long> LocationHotspots { get; set; } = new();
    public Dictionary<string, double> ResponseTimes { get; set; } = new();
    public Dictionary<string, long> ErrorCounts { get; set; } = new();
}

public class UserBehaviorDto
{
    public string UserHash { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
    public int SessionCount { get; set; }
    public TimeSpan AverageSessionDuration { get; set; }
    public Dictionary<string, int> PreferredCategories { get; set; } = new();
    public Dictionary<string, int> FilterUsage { get; set; } = new();
    public List<string> TopLocations { get; set; } = new();
    public double EngagementScore { get; set; }
}

public class BusinessMetricsDto
{
    public DateTime Date { get; set; }
    public long DailyActiveUsers { get; set; }
    public long WeeklyActiveUsers { get; set; }
    public long MonthlyActiveUsers { get; set; }
    public double RetentionRate { get; set; }
    public double ConversionRate { get; set; }
    public long SuggestionsGenerated { get; set; }
    public long SuccessfulInteractions { get; set; }
    public Dictionary<string, object> RevenueMetrics { get; set; } = new();
}

public class PerformanceMetricsDto
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> AverageResponseTimes { get; set; } = new();
    public Dictionary<string, long> RequestCounts { get; set; } = new();
    public Dictionary<string, double> ErrorRates { get; set; } = new();
    public Dictionary<string, long> CacheHitRates { get; set; } = new();
    public Dictionary<string, double> ResourceUtilization { get; set; } = new();
    public List<string> Alerts { get; set; } = new();
}

public class ContentAnalyticsDto
{
    public DateTime LastUpdated { get; set; }
    public long TotalPlaces { get; set; }
    public Dictionary<string, long> PlacesByCategory { get; set; } = new();
    public Dictionary<string, long> PlacesBySource { get; set; } = new();
    public Dictionary<string, double> AverageRatings { get; set; } = new();
    public long PlacesWithPhotos { get; set; }
    public long SponsoredPlaces { get; set; }
    public Dictionary<string, DateTime> LastDataRefresh { get; set; } = new();
}