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
    /// Background job that periodically updates user preference embeddings.
    /// Processes users with stale embeddings or sufficient new actions.
    /// </summary>
    public class PreferenceUpdateJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PreferenceUpdateJob> _logger;
        private readonly PreferenceUpdateJobOptions _options;

        public PreferenceUpdateJob(
            IServiceProvider serviceProvider,
            ILogger<PreferenceUpdateJob> logger,
            IOptions<PreferenceUpdateJobOptions> options)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PreferenceUpdateJob is starting. Interval: {Interval} minutes, Batch size: {BatchSize}",
                _options.IntervalMinutes, _options.BatchSize);

            // Wait for initial delay before first run
            await Task.Delay(TimeSpan.FromMinutes(_options.InitialDelayMinutes), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPreferenceUpdatesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PreferenceUpdateJob execution");
                }

                // Wait for next interval
                await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
            }

            _logger.LogInformation("PreferenceUpdateJob is stopping");
        }

        /// <summary>
        /// Processes a batch of users that need preference updates
        /// </summary>
        private async Task ProcessPreferenceUpdatesAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Starting preference update batch");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<WhatShouldIDoDbContext>();
                var preferenceLearningService = scope.ServiceProvider.GetRequiredService<IPreferenceLearningService>();
                var metricsService = scope.ServiceProvider.GetService<IMetricsService>();

                // Step 1: Find users that need embedding updates
                var staleThresholdDate = DateTime.UtcNow.AddDays(-_options.StaleEmbeddingDays);

                var usersToUpdate = await dbContext.UserProfiles
                    .AsNoTracking()
                    .Where(up =>
                        // Users with stale embeddings
                        (up.LastPreferenceUpdate == null || up.LastPreferenceUpdate < staleThresholdDate) ||
                        // Users with null embedding but have actions
                        (up.PreferenceEmbedding == null || up.PreferenceEmbedding.Length == 0))
                    .Select(up => new { up.UserId, up.LastPreferenceUpdate })
                    .Take(_options.BatchSize)
                    .ToListAsync(cancellationToken);

                if (!usersToUpdate.Any())
                {
                    _logger.LogInformation("No users found that need preference updates");
                    return;
                }

                _logger.LogInformation("Found {Count} users that need preference updates", usersToUpdate.Count);

                // Step 2: Process each user
                int successCount = 0;
                int failureCount = 0;
                int skippedCount = 0;

                foreach (var user in usersToUpdate)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Preference update batch cancelled");
                        break;
                    }

                    try
                    {
                        // Check if user has enough actions for meaningful embedding
                        var actionCount = await dbContext.UserActions
                            .CountAsync(a => a.UserId == user.UserId, cancellationToken);

                        if (actionCount < _options.MinimumActionsRequired)
                        {
                            _logger.LogDebug("User {UserId} has insufficient actions ({Count}), skipping",
                                user.UserId, actionCount);
                            skippedCount++;
                            continue;
                        }

                        // Update preferences and regenerate embedding
                        _logger.LogDebug("Updating preferences for user {UserId}", user.UserId);

                        await preferenceLearningService.UpdateUserPreferencesAsync(user.UserId, cancellationToken);
                        await preferenceLearningService.RegenerateUserEmbeddingAsync(user.UserId, cancellationToken);

                        successCount++;
                        _logger.LogDebug("Successfully updated preferences for user {UserId}", user.UserId);

                        // Small delay to avoid overwhelming the AI service
                        await Task.Delay(TimeSpan.FromMilliseconds(_options.DelayBetweenUpdateMs), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, "Failed to update preferences for user {UserId}", user.UserId);
                    }
                }

                var duration = (DateTime.UtcNow - startTime).TotalSeconds;

                _logger.LogInformation(
                    "Preference update batch completed in {Duration:F2}s. Success: {Success}, Failed: {Failed}, Skipped: {Skipped}",
                    duration, successCount, failureCount, skippedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing preference update batch");
                throw;
            }
        }
    }

    /// <summary>
    /// Configuration options for PreferenceUpdateJob
    /// </summary>
    public class PreferenceUpdateJobOptions
    {
        /// <summary>
        /// How often to run the job (in minutes). Default: 60 (every hour)
        /// </summary>
        public int IntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Initial delay before first run (in minutes). Default: 5
        /// </summary>
        public int InitialDelayMinutes { get; set; } = 5;

        /// <summary>
        /// Number of users to process in each batch. Default: 50
        /// </summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// Number of days before an embedding is considered stale. Default: 7
        /// </summary>
        public int StaleEmbeddingDays { get; set; } = 7;

        /// <summary>
        /// Minimum number of user actions required to generate embedding. Default: 5
        /// </summary>
        public int MinimumActionsRequired { get; set; } = 5;

        /// <summary>
        /// Delay between processing each user (milliseconds). Default: 500
        /// </summary>
        public int DelayBetweenUpdateMs { get; set; } = 500;

        /// <summary>
        /// Whether the job is enabled. Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
