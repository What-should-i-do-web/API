using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Health
{
    /// <summary>
    /// Health check for PostgreSQL connectivity and performance.
    /// Performs a simple query to verify the database is responsive.
    /// </summary>
    public class PostgresHealthCheck : IHealthCheck
    {
        private readonly WhatShouldIDoDbContext _dbContext;
        private readonly ILogger<PostgresHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostgresHealthCheck"/> class.
        /// </summary>
        /// <param name="dbContext">The database context</param>
        /// <param name="logger">The logger instance</param>
        public PostgresHealthCheck(
            WhatShouldIDoDbContext dbContext,
            ILogger<PostgresHealthCheck> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs the health check by executing a simple query.
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
                var stopwatch = Stopwatch.StartNew();

                // Perform a simple query to verify database connectivity
                // SELECT 1 is a lightweight query that verifies the connection
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT 1",
                    cancellationToken);

                stopwatch.Stop();
                var latencyMs = stopwatch.Elapsed.TotalMilliseconds;

                // Check connection pool statistics
                var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

                var data = new Dictionary<string, object>
                {
                    ["can_connect"] = canConnect,
                    ["latency_ms"] = latencyMs,
                    ["database"] = _dbContext.Database.GetDbConnection().Database
                };

                // Warn if latency is high (> 100ms for a simple query)
                if (latencyMs > 100)
                {
                    _logger.LogWarning("PostgreSQL latency is high: {Latency}ms", latencyMs);
                    return HealthCheckResult.Degraded(
                        $"PostgreSQL latency is high ({latencyMs:F2}ms)",
                        data: data);
                }

                if (!canConnect)
                {
                    _logger.LogError("PostgreSQL cannot connect");
                    return HealthCheckResult.Unhealthy(
                        "PostgreSQL cannot connect",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    $"PostgreSQL is healthy (latency: {latencyMs:F2}ms)",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostgreSQL health check failed");
                return HealthCheckResult.Unhealthy(
                    "PostgreSQL health check failed",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        ["can_connect"] = false,
                        ["error"] = ex.Message
                    });
            }
        }
    }
}
