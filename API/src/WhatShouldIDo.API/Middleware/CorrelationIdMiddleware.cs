using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Middleware
{
    /// <summary>
    /// Middleware that manages correlation IDs and initializes the observability context
    /// for each request. Propagates W3C trace context and adds correlation ID to responses.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;
        private readonly string _correlationIdHeader;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="observabilityOptions">Observability configuration options</param>
        public CorrelationIdMiddleware(
            RequestDelegate next,
            ILogger<CorrelationIdMiddleware> logger,
            IOptions<ObservabilityOptions> observabilityOptions)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _correlationIdHeader = observabilityOptions?.Value?.CorrelationIdHeader ?? "X-Correlation-Id";
        }

        /// <summary>
        /// Processes the HTTP request, setting up observability context and correlation IDs.
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <param name="observabilityContext">The scoped observability context</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task InvokeAsync(HttpContext context, IObservabilityContext observabilityContext)
        {
            // Extract or generate correlation ID
            string correlationId = observabilityContext.CorrelationId;
            if (context.Request.Headers.TryGetValue(_correlationIdHeader, out var headerValue) &&
                !string.IsNullOrWhiteSpace(headerValue))
            {
                // Use client-provided correlation ID if valid
                correlationId = headerValue.ToString();
            }

            // Add correlation ID to response headers
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(_correlationIdHeader))
                {
                    context.Response.Headers[_correlationIdHeader] = correlationId;
                }
                return Task.CompletedTask;
            });

            // Set up W3C trace context propagation
            var activity = Activity.Current;
            if (activity != null)
            {
                // Add correlation ID as baggage for distributed tracing
                activity.AddBaggage("correlation.id", correlationId);

                // Add to observability context
                observabilityContext.AddBaggage("correlation.id", correlationId);
            }

            // Extract and set user information if authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? context.User.FindFirst("sub")?.Value;

                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    observabilityContext.SetUserId(userId);

                    if (activity != null)
                    {
                        // Add user ID hash to activity tags (not raw user ID for privacy)
                        activity.SetTag("user.id_hash", observabilityContext.UserIdHash);
                    }
                }
            }

            // Set endpoint information
            var endpoint = context.GetEndpoint();
            if (endpoint != null)
            {
                var routePattern = endpoint.DisplayName ?? context.Request.Path;
                observabilityContext.SetEndpoint(routePattern);

                if (activity != null)
                {
                    activity.SetTag("http.route", routePattern);
                }
            }

            // Enrich logs with correlation ID using LogContext (if Serilog is configured)
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["TraceId"] = activity?.TraceId.ToString() ?? "unknown",
                ["UserId"] = observabilityContext.UserId?.ToString() ?? "anonymous",
                ["Endpoint"] = observabilityContext.Endpoint ?? "unknown"
            }))
            {
                _logger.LogDebug(
                    "Request started: {Method} {Path} (CorrelationId: {CorrelationId}, TraceId: {TraceId})",
                    context.Request.Method,
                    context.Request.Path,
                    correlationId,
                    activity?.TraceId.ToString() ?? "unknown");

                await _next(context);

                _logger.LogDebug(
                    "Request completed: {Method} {Path} with status {StatusCode} (CorrelationId: {CorrelationId})",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    correlationId);
            }
        }
    }
}
