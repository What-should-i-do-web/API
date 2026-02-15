using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background job that cleans up old user actions to prevent database bloat.
    /// Keeps recent actions and aggregates older ones for analytics.
    /// </summary>
    public class UserActionCleanupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserActionCleanupJob> _logger;
        private readonly UserActionCleanupJobOptions _options;

        public UserActionCleanupJob(
            IServiceProvider serviceProvider,
            ILogger<UserActionCleanupJob> logger,
            IOptions<UserActionCleanupJobOptions> options)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("UserActionCleanupJob is disabled");
                return;
            }

            _logger.LogInformation("UserActionCleanupJob is starting. Interval: {Interval} hours",
                _options.IntervalHours);

            // Wait for initial delay before first run
            await Task.Delay(TimeSpan.FromHours(_options.InitialDelayHours), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOldActionsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in UserActionCleanupJob execution");
                }

                // Wait for next interval
                await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
            }

            _logger.LogInformation("UserActionCleanupJob is stopping");
        }

        /// <summary>
        /// Cleans up old user actions based on retention policy
        /// </summary>
        private async Task CleanupOldActionsAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Starting user action cleanup");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<WhatShouldIDoDbContext>();

                var retentionThresholdDate = DateTime.UtcNow.AddDays(-_options.RetentionDays);

                // Count actions to be deleted
                var actionsToDeleteCount = await dbContext.UserActions
                    .CountAsync(a => a.ActionTimestamp < retentionThresholdDate, cancellationToken);

                if (actionsToDeleteCount == 0)
                {
                    _logger.LogInformation("No old user actions found for cleanup");
                    return;
                }

                _logger.LogInformation("Found {Count} user actions older than {Days} days to clean up",
                    actionsToDeleteCount, _options.RetentionDays);

                // Delete in batches to avoid locking the database for too long
                int totalDeleted = 0;
                int batchSize = 1000;

                while (totalDeleted < actionsToDeleteCount && !cancellationToken.IsCancellationRequested)
                {
                    var actionsToDelete = await dbContext.UserActions
                        .Where(a => a.ActionTimestamp < retentionThresholdDate)
                        .OrderBy(a => a.ActionTimestamp)
                        .Take(batchSize)
                        .ToListAsync(cancellationToken);

                    if (!actionsToDelete.Any())
                        break;

                    dbContext.UserActions.RemoveRange(actionsToDelete);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    totalDeleted += actionsToDelete.Count;

                    _logger.LogDebug("Deleted batch of {Count} user actions (total: {Total})",
                        actionsToDelete.Count, totalDeleted);

                    // Small delay between batches
                    await Task.Delay(100, cancellationToken);
                }

                var duration = (DateTime.UtcNow - startTime).TotalSeconds;

                _logger.LogInformation(
                    "User action cleanup completed in {Duration:F2}s. Deleted {Count} actions",
                    duration, totalDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user action cleanup");
                throw;
            }
        }
    }

    /// <summary>
    /// Configuration options for UserActionCleanupJob
    /// </summary>
    public class UserActionCleanupJobOptions
    {
        /// <summary>
        /// How often to run the job (in hours). Default: 24 (daily)
        /// </summary>
        public int IntervalHours { get; set; } = 24;

        /// <summary>
        /// Initial delay before first run (in hours). Default: 1
        /// </summary>
        public int InitialDelayHours { get; set; } = 1;

        /// <summary>
        /// Number of days to retain user actions. Default: 180 (6 months)
        /// </summary>
        public int RetentionDays { get; set; } = 180;

        /// <summary>
        /// Whether the job is enabled. Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
