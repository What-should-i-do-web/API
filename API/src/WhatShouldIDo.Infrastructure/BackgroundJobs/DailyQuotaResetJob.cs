using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background job that resets user quotas daily at a configurable time.
    /// Supports both Redis and in-memory quota stores.
    /// </summary>
    public class DailyQuotaResetJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyQuotaResetJob> _logger;
        private readonly DailyQuotaResetJobOptions _options;

        public DailyQuotaResetJob(
            IServiceProvider serviceProvider,
            ILogger<DailyQuotaResetJob> logger,
            IOptions<DailyQuotaResetJobOptions> options)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("DailyQuotaResetJob is disabled");
                return;
            }

            _logger.LogInformation("DailyQuotaResetJob is starting. Reset time: {ResetTime} UTC, Default quota: {DefaultQuota}",
                _options.ResetTimeUtc, _options.DefaultFreeQuota);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Calculate time until next reset
                    var now = DateTime.UtcNow;
                    var nextResetTime = GetNextResetTime(now);
                    var delay = nextResetTime - now;

                    if (delay.TotalMilliseconds > 0)
                    {
                        _logger.LogInformation("Next quota reset scheduled at {NextReset} UTC (in {Hours:F1} hours)",
                            nextResetTime, delay.TotalHours);

                        // Wait until reset time
                        await Task.Delay(delay, stoppingToken);
                    }

                    // Execute reset
                    await ExecuteQuotaResetAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("DailyQuotaResetJob is stopping due to cancellation");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in DailyQuotaResetJob execution");

                    // Wait before retrying on error
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("DailyQuotaResetJob has stopped");
        }

        /// <summary>
        /// Calculate the next reset time based on configured time
        /// </summary>
        private DateTime GetNextResetTime(DateTime now)
        {
            var todayReset = now.Date.Add(_options.ResetTimeUtc);

            // If today's reset time has passed, schedule for tomorrow
            if (now >= todayReset)
            {
                return todayReset.AddDays(1);
            }

            return todayReset;
        }

        /// <summary>
        /// Execute the quota reset for all free users
        /// </summary>
        private async Task ExecuteQuotaResetAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Starting daily quota reset at {Time} UTC", startTime);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<WhatShouldIDoDbContext>();
                var quotaStore = scope.ServiceProvider.GetRequiredService<IQuotaStore>();
                var metricsService = scope.ServiceProvider.GetService<IMetricsService>();

                // Get all free users (SubscriptionTier = 0 / Free)
                var freeUsers = await dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.SubscriptionTier == Domain.Entities.SubscriptionTier.Free)
                    .Select(u => u.Id)
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Found {Count} free users for quota reset", freeUsers.Count);

                int successCount = 0;
                int failureCount = 0;

                // Process in batches to avoid overwhelming the quota store
                var batches = freeUsers.Chunk(_options.BatchSize);

                foreach (var batch in batches)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Quota reset cancelled");
                        break;
                    }

                    foreach (var userId in batch)
                    {
                        try
                        {
                            await quotaStore.SetAsync(userId, _options.DefaultFreeQuota, cancellationToken);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            failureCount++;
                            _logger.LogError(ex, "Failed to reset quota for user {UserId}", userId);
                        }
                    }

                    // Small delay between batches to avoid Redis overload
                    if (_options.DelayBetweenBatchesMs > 0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(_options.DelayBetweenBatchesMs), cancellationToken);
                    }
                }

                var duration = (DateTime.UtcNow - startTime).TotalSeconds;

                _logger.LogInformation(
                    "Daily quota reset completed in {Duration:F2}s. Success: {Success}, Failed: {Failed}",
                    duration, successCount, failureCount);

                // Record metrics
                metricsService?.IncrementCounter("quota_reset_total", new[]
                {
                    new KeyValuePair<string, object?>("status", "completed")
                });

                metricsService?.RecordHistogram("quota_reset_duration_seconds", duration, new[]
                {
                    new KeyValuePair<string, object?>("users_count", successCount)
                });

                if (failureCount > 0)
                {
                    metricsService?.IncrementCounter("quota_reset_failures_total", new[]
                    {
                        new KeyValuePair<string, object?>("count", failureCount)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during daily quota reset");
                throw;
            }
        }

        /// <summary>
        /// Manually trigger quota reset (for admin/testing purposes)
        /// </summary>
        public async Task TriggerManualResetAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Manual quota reset triggered");
            await ExecuteQuotaResetAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Configuration options for DailyQuotaResetJob
    /// </summary>
    public class DailyQuotaResetJobOptions
    {
        /// <summary>
        /// Whether the job is enabled. Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Time of day (UTC) to reset quotas. Default: 00:00 (midnight UTC)
        /// </summary>
        public TimeSpan ResetTimeUtc { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Default quota value for free users. Default: 5
        /// </summary>
        public int DefaultFreeQuota { get; set; } = 5;

        /// <summary>
        /// Number of users to process in each batch. Default: 100
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// Delay between batches in milliseconds. Default: 100
        /// </summary>
        public int DelayBetweenBatchesMs { get; set; } = 100;
    }
}
