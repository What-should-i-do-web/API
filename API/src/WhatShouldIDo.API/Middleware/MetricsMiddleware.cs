using System.Diagnostics;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Middleware
{
    /// <summary>
    /// Middleware that records detailed metrics for all HTTP requests.
    /// Captures request duration, status codes, user context, and OpenTelemetry spans.
    /// </summary>
    public class MetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MetricsMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="logger">The logger instance</param>
        public MetricsMiddleware(
            RequestDelegate next,
            ILogger<MetricsMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes the HTTP request and records comprehensive metrics.
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <param name="metricsService">The metrics service (injected per request)</param>
        /// <param name="observabilityContext">The observability context (injected per request)</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task InvokeAsync(
            HttpContext context,
            IMetricsService metricsService,
            IObservabilityContext observabilityContext)
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
                var durationMs = stopwatch.Elapsed.TotalMilliseconds;
                var statusCode = context.Response.StatusCode;

                // Normalize endpoint path for metrics (avoid high cardinality)
                var normalizedPath = NormalizeEndpoint(path);

                // Get user context from observability context
                var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
                var isPremium = observabilityContext.IsPremium;

                try
                {
                    // Record comprehensive request metrics
                    metricsService.RecordRequest(
                        normalizedPath,
                        method,
                        statusCode,
                        durationMs,
                        isAuthenticated,
                        isPremium);

                    // Log slow requests (warning threshold: 1s)
                    if (durationMs > 1000)
                    {
                        _logger.LogWarning(
                            "Slow request detected: {Method} {Endpoint} took {Duration}ms (Status: {StatusCode}, Premium: {IsPremium})",
                            method,
                            normalizedPath,
                            durationMs,
                            statusCode,
                            isPremium?.ToString() ?? "unknown");
                    }

                    // Enrich OpenTelemetry span with additional attributes
                    var activity = Activity.Current;
                    if (activity != null)
                    {
                        activity.SetTag("http.status_code", statusCode);
                        activity.SetTag("http.request_duration_ms", durationMs);
                        activity.SetTag("user.authenticated", isAuthenticated);
                        if (isPremium.HasValue)
                        {
                            activity.SetTag("user.is_premium", isPremium.Value);
                        }

                        // Mark activity as error if 5xx status code
                        if (statusCode >= 500)
                        {
                            activity.SetStatus(ActivityStatusCode.Error, $"HTTP {statusCode}");
                        }
                    }
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