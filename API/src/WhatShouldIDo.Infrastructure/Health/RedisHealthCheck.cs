using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WhatShouldIDo.Infrastructure.Health
{
    /// <summary>
    /// Health check for Redis connectivity and performance.
    /// Performs a PING command to verify Redis is responsive.
    /// </summary>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisHealthCheck"/> class.
        /// </summary>
        /// <param name="redis">The Redis connection multiplexer</param>
        /// <param name="logger">The logger instance</param>
        public RedisHealthCheck(
            IConnectionMultiplexer redis,
            ILogger<RedisHealthCheck> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs the health check by pinging Redis.
        /// </summary>
        /// <param name="context">The health check context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_redis.IsConnected)
                {
                    _logger.LogWarning("Redis is not connected");
                    return HealthCheckResult.Unhealthy(
                        "Redis is not connected",
                        data: new Dictionary<string, object>
                        {
                            ["connected"] = false
                        });
                }

                var database = _redis.GetDatabase();
                var startTime = DateTime.UtcNow;

                // Perform a PING to verify Redis is responsive
                var pingResult = await database.PingAsync();
                var latencyMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                var data = new Dictionary<string, object>
                {
                    ["connected"] = true,
                    ["latency_ms"] = latencyMs,
                    ["ping_response_ms"] = pingResult.TotalMilliseconds
                };

                // Warn if latency is high (> 50ms)
                if (latencyMs > 50)
                {
                    _logger.LogWarning("Redis latency is high: {Latency}ms", latencyMs);
                    return HealthCheckResult.Degraded(
                        $"Redis latency is high ({latencyMs:F2}ms)",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    $"Redis is healthy (latency: {latencyMs:F2}ms)",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed");
                return HealthCheckResult.Unhealthy(
                    "Redis health check failed",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        ["connected"] = false,
                        ["error"] = ex.Message
                    });
            }
        }
    }
}
