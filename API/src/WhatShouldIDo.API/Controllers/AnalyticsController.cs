using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.Services;

namespace WhatShouldIDo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<Dictionary<string, object>>> GetDashboard([FromQuery] string? userHash = null)
    {
        try
        {
            var dashboardData = await _analyticsService.GetDashboardDataAsync(userHash);
            return Ok(dashboardData);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get dashboard data", details = ex.Message });
        }
    }

    [HttpGet("health")]
    public async Task<ActionResult> GetSystemHealth()
    {
        try
        {
            var health = await _analyticsService.GetSystemHealthAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get system health", details = ex.Message });
        }
    }

    [HttpGet("usage")]
    public async Task<ActionResult> GetUsageAnalytics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.Date.AddDays(-7);
            var end = endDate ?? DateTime.UtcNow.Date;

            var usage = await _analyticsService.GetUsageAnalyticsAsync(start, end);
            return Ok(usage);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get usage analytics", details = ex.Message });
        }
    }

    [HttpGet("user/{userHash}")]
    public async Task<ActionResult> GetUserBehavior(string userHash)
    {
        try
        {
            var behavior = await _analyticsService.GetUserBehaviorAsync(userHash);
            return Ok(behavior);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get user behavior", details = ex.Message });
        }
    }

    [HttpGet("business")]
    public async Task<ActionResult> GetBusinessMetrics([FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow.Date;
            var metrics = await _analyticsService.GetBusinessMetricsAsync(targetDate);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get business metrics", details = ex.Message });
        }
    }

    [HttpGet("performance")]
    public async Task<ActionResult> GetPerformanceMetrics([FromQuery] int hours = 1)
    {
        try
        {
            var period = TimeSpan.FromHours(hours);
            var metrics = await _analyticsService.GetPerformanceMetricsAsync(period);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get performance metrics", details = ex.Message });
        }
    }

    [HttpGet("content")]
    public async Task<ActionResult> GetContentAnalytics()
    {
        try
        {
            var analytics = await _analyticsService.GetContentAnalyticsAsync();
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get content analytics", details = ex.Message });
        }
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<List<string>>> GetActiveAlerts()
    {
        try
        {
            var alerts = await _analyticsService.GetActiveAlertsAsync();
            return Ok(new { alerts, count = alerts.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get alerts", details = ex.Message });
        }
    }

    [HttpGet("realtime")]
    public async Task<ActionResult<Dictionary<string, object>>> GetRealTimeMetrics()
    {
        try
        {
            var metrics = await _analyticsService.GetRealTimeMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get real-time metrics", details = ex.Message });
        }
    }

    [HttpPost("events")]
    public async Task<ActionResult> TrackEvent([FromBody] TrackEventRequest request)
    {
        try
        {
            await _analyticsService.TrackEventAsync(request.EventName, request.UserHash, request.Properties);
            return Ok(new { success = true, message = "Event tracked successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to track event", details = ex.Message });
        }
    }

    [HttpPost("interactions")]
    public async Task<ActionResult> TrackUserInteraction([FromBody] TrackInteractionRequest request)
    {
        try
        {
            await _analyticsService.TrackUserInteractionAsync(request.UserHash, request.InteractionType, request.Data);
            return Ok(new { success = true, message = "Interaction tracked successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to track interaction", details = ex.Message });
        }
    }

    [HttpPost("errors")]
    public async Task<ActionResult> TrackError([FromBody] TrackErrorRequest request)
    {
        try
        {
            await _analyticsService.TrackErrorAsync(request.Endpoint, request.ErrorType, request.Message, request.UserHash);
            return Ok(new { success = true, message = "Error tracked successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to track error", details = ex.Message });
        }
    }

    [HttpGet("export")]
    public async Task<ActionResult> ExportAnalytics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string format = "json")
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.Date.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow.Date;

            var data = new
            {
                export_date = DateTime.UtcNow,
                period_start = start,
                period_end = end,
                usage = await _analyticsService.GetUsageAnalyticsAsync(start, end),
                content = await _analyticsService.GetContentAnalyticsAsync(),
                performance = await _analyticsService.GetPerformanceMetricsAsync(end - start)
            };

            if (format.ToLower() == "csv")
            {
                // In production, implement CSV conversion
                return BadRequest("CSV export not implemented yet");
            }

            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to export analytics", details = ex.Message });
        }
    }
}

public class TrackEventRequest
{
    public string EventName { get; set; } = string.Empty;
    public string UserHash { get; set; } = string.Empty;
    public Dictionary<string, object>? Properties { get; set; }
}

public class TrackInteractionRequest
{
    public string UserHash { get; set; } = string.Empty;
    public string InteractionType { get; set; } = string.Empty;
    public Dictionary<string, object>? Data { get; set; }
}

public class TrackErrorRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? UserHash { get; set; }
}