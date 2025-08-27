using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Services;

public interface IAnalyticsService
{
    Task<SystemHealthDto> GetSystemHealthAsync();
    Task<UsageAnalyticsDto> GetUsageAnalyticsAsync(DateTime startDate, DateTime endDate);
    Task<UserBehaviorDto> GetUserBehaviorAsync(string userHash);
    Task<BusinessMetricsDto> GetBusinessMetricsAsync(DateTime date);
    Task<PerformanceMetricsDto> GetPerformanceMetricsAsync(TimeSpan period);
    Task<ContentAnalyticsDto> GetContentAnalyticsAsync();
    
    Task TrackEventAsync(string eventName, string userHash, Dictionary<string, object>? properties = null);
    Task TrackApiCallAsync(string endpoint, string userHash, TimeSpan responseTime, bool success);
    Task TrackErrorAsync(string endpoint, string errorType, string message, string? userHash = null);
    Task TrackUserInteractionAsync(string userHash, string interactionType, Dictionary<string, object>? data = null);
    
    Task<Dictionary<string, object>> GetDashboardDataAsync(string? userHash = null);
    Task<List<string>> GetActiveAlertsAsync();
    Task<Dictionary<string, object>> GetRealTimeMetricsAsync();
}