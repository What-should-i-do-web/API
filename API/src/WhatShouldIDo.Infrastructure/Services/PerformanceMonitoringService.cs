using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using WhatShouldIDo.Infrastructure.Data;
using WhatShouldIDo.Infrastructure.Interceptors;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Caching;

namespace WhatShouldIDo.Infrastructure.Services
{
    public interface IPerformanceMonitoringService
    {
        Task<SystemPerformanceReport> GetSystemPerformanceAsync();
        Task<DatabasePerformanceReport> GetDatabasePerformanceAsync();
        PerformanceReport GetQueryPerformance();
        Task<bool> PerformHealthCheckAsync();
    }

    public class PerformanceMonitoringService : IPerformanceMonitoringService
    {
        private readonly WhatShouldIDoDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private static readonly Process _currentProcess = Process.GetCurrentProcess();

        public PerformanceMonitoringService(
            WhatShouldIDoDbContext dbContext, 
            ICacheService cacheService,
            ILogger<PerformanceMonitoringService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<SystemPerformanceReport> GetSystemPerformanceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Refresh process info
                _currentProcess.Refresh();
                
                var report = new SystemPerformanceReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    ProcessorTimeMs = _currentProcess.TotalProcessorTime.TotalMilliseconds,
                    WorkingSetMB = _currentProcess.WorkingSet64 / (1024 * 1024),
                    PrivateMemoryMB = _currentProcess.PrivateMemorySize64 / (1024 * 1024),
                    VirtualMemoryMB = _currentProcess.VirtualMemorySize64 / (1024 * 1024),
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount
                };

                // Test cache performance
                var cacheTestKey = $"perf_test_{Guid.NewGuid()}";
                var cacheTestValue = "test_value";
                var cacheStopwatch = Stopwatch.StartNew();
                
                var cachedValue = await _cacheService.GetOrSetAsync(cacheTestKey, 
                    async () => cacheTestValue, TimeSpan.FromMinutes(1));
                await _cacheService.RemoveAsync(cacheTestKey);
                
                cacheStopwatch.Stop();
                report.CacheResponseTimeMs = cacheStopwatch.ElapsedMilliseconds;
                report.CacheHealthy = cachedValue == cacheTestValue;

                stopwatch.Stop();
                report.ReportGenerationTimeMs = stopwatch.ElapsedMilliseconds;

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating system performance report");
                
                return new SystemPerformanceReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    ReportGenerationTimeMs = stopwatch.ElapsedMilliseconds,
                    CacheHealthy = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<DatabasePerformanceReport> GetDatabasePerformanceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var report = new DatabasePerformanceReport
                {
                    GeneratedAt = DateTime.UtcNow
                };

                // Test database connectivity and basic query performance
                var dbTestStopwatch = Stopwatch.StartNew();
                var placesCount = await _dbContext.Places.CountAsync();
                dbTestStopwatch.Stop();
                report.DatabaseResponseTimeMs = dbTestStopwatch.ElapsedMilliseconds;

                // Get record counts
                report.PlacesCount = placesCount;
                report.SuggestionsCount = await _dbContext.Suggestions.CountAsync();
                report.UsersCount = await _dbContext.Users.CountAsync();
                report.UserVisitsCount = await _dbContext.UserVisits.CountAsync();

                // Test spatial query performance (core functionality)
                var spatialStopwatch = Stopwatch.StartNew();
                var testLat = 41.0082f;
                var testLng = 28.9784f;
                var nearbyPlacesCount = await _dbContext.Places
                    .Where(p => Math.Abs(p.Latitude - testLat) < 0.01f && Math.Abs(p.Longitude - testLng) < 0.01f)
                    .CountAsync();
                spatialStopwatch.Stop();
                report.SpatialQueryResponseTimeMs = spatialStopwatch.ElapsedMilliseconds;

                // Test cache vs database performance
                var cacheKey = "perf_test_places_count";
                var cacheStopwatch = Stopwatch.StartNew();
                var cachedCount = await _cacheService.GetOrSetAsync(cacheKey,
                    async () => placesCount, TimeSpan.FromMinutes(5));
                cacheStopwatch.Stop();
                
                report.CacheVsDbRatio = dbTestStopwatch.ElapsedMilliseconds / (double)Math.Max(cacheStopwatch.ElapsedMilliseconds, 1);
                report.DatabaseHealthy = true;

                stopwatch.Stop();
                report.ReportGenerationTimeMs = stopwatch.ElapsedMilliseconds;

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating database performance report");
                
                return new DatabasePerformanceReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    ReportGenerationTimeMs = stopwatch.ElapsedMilliseconds,
                    DatabaseHealthy = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public PerformanceReport GetQueryPerformance()
        {
            return QueryPerformanceMetrics.GetPerformanceReport();
        }

        public async Task<bool> PerformHealthCheckAsync()
        {
            try
            {
                // Test database
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                
                // Test cache
                var testKey = "health_check";
                var result = await _cacheService.GetOrSetAsync(testKey, 
                    async () => "ok", TimeSpan.FromMinutes(1));
                await _cacheService.RemoveAsync(testKey);
                
                return result == "ok";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return false;
            }
        }
    }

    public class SystemPerformanceReport
    {
        public DateTime GeneratedAt { get; set; }
        public double ProcessorTimeMs { get; set; }
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public long VirtualMemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long CacheResponseTimeMs { get; set; }
        public bool CacheHealthy { get; set; }
        public long ReportGenerationTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class DatabasePerformanceReport
    {
        public DateTime GeneratedAt { get; set; }
        public long DatabaseResponseTimeMs { get; set; }
        public long SpatialQueryResponseTimeMs { get; set; }
        public double CacheVsDbRatio { get; set; }
        public int PlacesCount { get; set; }
        public int SuggestionsCount { get; set; }
        public int UsersCount { get; set; }
        public int UserVisitsCount { get; set; }
        public bool DatabaseHealthy { get; set; }
        public long ReportGenerationTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
    }
}