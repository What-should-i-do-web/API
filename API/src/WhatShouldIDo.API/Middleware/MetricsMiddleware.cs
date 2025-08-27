using System.Diagnostics;
using WhatShouldIDo.Infrastructure.Services;

namespace WhatShouldIDo.API.Middleware
{
    public class MetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<MetricsMiddleware> _logger;

        public MetricsMiddleware(
            RequestDelegate next,
            IMetricsService metricsService,
            ILogger<MetricsMiddleware> logger)
        {
            _next = next;
            _metricsService = metricsService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var path = context.Request.Path.ToString();
            var method = context.Request.Method;

            // Skip metrics for certain paths
            if (ShouldSkipMetrics(path))
            {
                await _next(context);
                return;
            }

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed.TotalSeconds;
                var statusCode = context.Response.StatusCode;

                // Normalize endpoint path for metrics
                var normalizedPath = NormalizeEndpoint(path);

                try
                {
                    _metricsService.RecordApiRequest(normalizedPath, method, statusCode, duration);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error recording API request metrics for {Path}", path);
                }
            }
        }

        private static bool ShouldSkipMetrics(string path)
        {
            var pathsToSkip = new[]
            {
                "/metrics",
                "/health",
                "/favicon.ico",
                "/_framework",
                "/swagger"
            };

            return pathsToSkip.Any(skip => path.StartsWith(skip, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeEndpoint(string path)
        {
            // Convert dynamic paths to normalized patterns
            // e.g., /api/discover/123 -> /api/discover/{id}
            
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var normalized = new List<string>();

            foreach (var segment in segments)
            {
                // Check if segment looks like an ID (numeric, GUID, etc.)
                if (IsId(segment))
                {
                    normalized.Add("{id}");
                }
                else if (IsCoordinate(segment))
                {
                    normalized.Add("{coord}");
                }
                else
                {
                    normalized.Add(segment);
                }
            }

            return "/" + string.Join("/", normalized);
        }

        private static bool IsId(string segment)
        {
            // Check for numeric ID
            if (long.TryParse(segment, out _))
                return true;

            // Check for GUID
            if (Guid.TryParse(segment, out _))
                return true;

            // Check for common ID patterns
            if (segment.Length > 10 && (segment.All(char.IsLetterOrDigit) || segment.Contains('-')))
                return true;

            return false;
        }

        private static bool IsCoordinate(string segment)
        {
            // Check if it looks like a coordinate (decimal number)
            return double.TryParse(segment, out var value) && 
                   Math.Abs(value) <= 180; // Valid lat/lng range
        }
    }
}