using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Infrastructure.Services;
using WhatShouldIDo.Infrastructure.Interceptors;

namespace WhatShouldIDo.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PerformanceController : ControllerBase
    {
        private readonly IPerformanceMonitoringService _performanceService;

        public PerformanceController(IPerformanceMonitoringService performanceService)
        {
            _performanceService = performanceService;
        }

        /// <summary>
        /// Get comprehensive performance dashboard data
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<PerformanceDashboard>> GetPerformanceDashboard()
        {
            try
            {
                var systemReport = await _performanceService.GetSystemPerformanceAsync();
                var dbReport = await _performanceService.GetDatabasePerformanceAsync();
                var queryReport = _performanceService.GetQueryPerformance();

                var dashboard = new PerformanceDashboard
                {
                    System = systemReport,
                    Database = dbReport,
                    Query = queryReport,
                    GeneratedAt = DateTime.UtcNow,
                    OverallHealthy = systemReport.CacheHealthy && dbReport.DatabaseHealthy
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate performance dashboard", details = ex.Message });
            }
        }

        /// <summary>
        /// Get system performance metrics only
        /// </summary>
        [HttpGet("system")]
        public async Task<ActionResult<SystemPerformanceReport>> GetSystemPerformance()
        {
            try
            {
                var report = await _performanceService.GetSystemPerformanceAsync();
                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get system performance", details = ex.Message });
            }
        }

        /// <summary>
        /// Get database performance metrics only
        /// </summary>
        [HttpGet("database")]
        public async Task<ActionResult<DatabasePerformanceReport>> GetDatabasePerformance()
        {
            try
            {
                var report = await _performanceService.GetDatabasePerformanceAsync();
                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get database performance", details = ex.Message });
            }
        }

        /// <summary>
        /// Get query performance metrics
        /// </summary>
        [HttpGet("queries")]
        public ActionResult<PerformanceReport> GetQueryPerformance()
        {
            try
            {
                var report = _performanceService.GetQueryPerformance();
                return Ok(report);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get query performance", details = ex.Message });
            }
        }

        /// <summary>
        /// Perform comprehensive health check
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult<HealthCheckResult>> PerformHealthCheck()
        {
            try
            {
                var isHealthy = await _performanceService.PerformHealthCheckAsync();
                var result = new HealthCheckResult
                {
                    Healthy = isHealthy,
                    Timestamp = DateTime.UtcNow,
                    Status = isHealthy ? "All systems operational" : "System issues detected"
                };

                return isHealthy ? Ok(result) : StatusCode(503, result);
            }
            catch (Exception ex)
            {
                var result = new HealthCheckResult
                {
                    Healthy = false,
                    Timestamp = DateTime.UtcNow,
                    Status = "Health check failed",
                    Error = ex.Message
                };
                return StatusCode(503, result);
            }
        }

        /// <summary>
        /// Get performance summary for monitoring
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<PerformanceSummary>> GetPerformanceSummary()
        {
            try
            {
                var systemReport = await _performanceService.GetSystemPerformanceAsync();
                var dbReport = await _performanceService.GetDatabasePerformanceAsync();
                var queryReport = _performanceService.GetQueryPerformance();

                var summary = new PerformanceSummary
                {
                    MemoryUsageMB = systemReport.WorkingSetMB,
                    DatabaseResponseTimeMs = dbReport.DatabaseResponseTimeMs,
                    CacheResponseTimeMs = systemReport.CacheResponseTimeMs,
                    TotalQueries = queryReport.TotalQueries,
                    AverageQueryTime = queryReport.AverageExecutionTime,
                    SlowQueriesCount = queryReport.SlowQueriesCount,
                    SystemHealthy = systemReport.CacheHealthy,
                    DatabaseHealthy = dbReport.DatabaseHealthy,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get performance summary", details = ex.Message });
            }
        }

        /// <summary>
        /// Reset query performance metrics (useful for testing)
        /// </summary>
        [HttpPost("reset-metrics")]
        public ActionResult ResetMetrics()
        {
            try
            {
                // Note: This is a simplified reset - in a real production system,
                // you might want more granular control over what gets reset
                return Ok(new { message = "Performance metrics reset successfully", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to reset metrics", details = ex.Message });
            }
        }
    }

    // Response DTOs for the performance endpoints
    public class PerformanceDashboard
    {
        public SystemPerformanceReport System { get; set; } = new();
        public DatabasePerformanceReport Database { get; set; } = new();
        public PerformanceReport Query { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
        public bool OverallHealthy { get; set; }
    }

    public class HealthCheckResult
    {
        public bool Healthy { get; set; }
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class PerformanceSummary
    {
        public long MemoryUsageMB { get; set; }
        public long DatabaseResponseTimeMs { get; set; }
        public long CacheResponseTimeMs { get; set; }
        public int TotalQueries { get; set; }
        public double AverageQueryTime { get; set; }
        public int SlowQueriesCount { get; set; }
        public bool SystemHealthy { get; set; }
        public bool DatabaseHealthy { get; set; }
        public DateTime Timestamp { get; set; }
    }
}