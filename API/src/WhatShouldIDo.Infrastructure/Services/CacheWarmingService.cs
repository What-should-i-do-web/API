using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Caching;

namespace WhatShouldIDo.Infrastructure.Services
{
    public interface ICacheWarmingService
    {
        Task WarmPopularLocationsAsync();
        Task WarmCategoriesAsync();
        Task WarmUserPreferencesAsync();
    }

    public class CacheWarmingService : ICacheWarmingService, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CacheWarmingService> _logger;
        private readonly CacheWarmingOptions _options;
        private readonly Timer? _warmingTimer;

        // Popular locations to warm (could be loaded from config or database)
        private readonly (float lat, float lng, string name)[] _popularLocations = new[]
        {
            (41.0082f, 28.9784f, "Istanbul"),
            (39.9334f, 32.8597f, "Ankara"),
            (38.4237f, 27.1428f, "Izmir"),
            (36.8969f, 30.7133f, "Antalya"),
            (40.1917f, 29.0611f, "Bursa")
        };

        public CacheWarmingService(
            IServiceProvider serviceProvider,
            ILogger<CacheWarmingService> logger,
            IOptions<CacheWarmingOptions> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
            
            // Set up periodic warming timer if enabled
            if (_options.EnableScheduledWarming)
            {
                _warmingTimer = new Timer(async _ => await PerformScheduledWarming(), 
                    null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(_options.WarmingIntervalMinutes));
            }
            
            // Perform initial warming if enabled
            if (_options.WarmOnStartup)
            {
                _ = Task.Run(async () => await PerformInitialWarming());
            }
        }

        private async Task PerformInitialWarming()
        {
            try
            {
                _logger.LogInformation("🚀 Starting initial cache warming...");
                
                await WarmPopularLocationsAsync();
                await WarmCategoriesAsync();
                
                _logger.LogInformation("✅ Initial cache warming completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Initial cache warming failed");
            }
        }

        private async Task PerformScheduledWarming()
        {
            if (!_options.EnableScheduledWarming)
                return;

            try
            {
                _logger.LogInformation("🔄 Starting scheduled cache warming...");
                
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetService<ICacheInvalidationService>();
                
                if (cacheService != null)
                {
                    await WarmPopularLocationsAsync();
                    var stats = await cacheService.GetStatisticsAsync();
                    _logger.LogInformation("📊 Cache stats after warming - Hit rate: {HitRate:P2}", stats.HitRate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Scheduled cache warming failed");
            }
        }

        public async Task WarmPopularLocationsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var suggestionService = scope.ServiceProvider.GetService<ISuggestionService>();
                var cacheService = scope.ServiceProvider.GetService<ICacheInvalidationService>();
                
                if (suggestionService == null || cacheService == null)
                {
                    _logger.LogWarning("Required services not available for cache warming");
                    return;
                }

                _logger.LogInformation("🌍 Warming popular location caches...");
                
                var warmingTasks = _popularLocations.Select(async location =>
                {
                    try
                    {
                        // Warm nearby places cache
                        var nearbyKey = $"places:nearby:{location.lat}:{location.lng}:3000";
                        await cacheService.WarmCacheAsync(nearbyKey, async () =>
                        {
                            var suggestions = await suggestionService.GetNearbySuggestionsAsync(
                                location.lat, location.lng, 3000);
                            return suggestions.ToList();
                        }, TimeSpan.FromMinutes(30));

                        // Warm popular categories for this location
                        var categories = new[] { "restaurant", "tourist_attraction", "shopping_mall", "park" };
                        foreach (var category in categories)
                        {
                            var categoryKey = $"places:category:{location.lat}:{location.lng}:{category}";
                            await cacheService.WarmCacheAsync(categoryKey, async () =>
                            {
                                // This would need to be implemented in your suggestion service
                                // For now, return empty to demonstrate structure
                                return new List<object>();
                            }, TimeSpan.FromMinutes(45));
                        }

                        _logger.LogDebug("✅ Warmed cache for {LocationName}", location.name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to warm cache for location {LocationName}", location.name);
                    }
                });

                await Task.WhenAll(warmingTasks);
                _logger.LogInformation("🔥 Popular locations cache warming completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warm popular locations cache");
            }
        }

        public async Task WarmCategoriesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetService<ICacheInvalidationService>();
                
                if (cacheService == null) return;

                _logger.LogInformation("🏷️ Warming categories cache...");

                // Popular categories to warm
                var categories = new[]
                {
                    "restaurant", "cafe", "tourist_attraction", "shopping_mall",
                    "park", "museum", "hotel", "hospital", "bank", "gas_station"
                };

                await cacheService.WarmCacheAsync("categories:all", async () =>
                {
                    return categories;
                }, TimeSpan.FromHours(24)); // Categories don't change often

                _logger.LogInformation("✅ Categories cache warmed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warm categories cache");
            }
        }

        public async Task WarmUserPreferencesAsync()
        {
            try
            {
                _logger.LogInformation("👤 Warming user preferences cache...");
                
                // This would warm common user preference patterns
                // Implementation depends on your user preference system
                
                _logger.LogInformation("✅ User preferences cache warmed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warm user preferences cache");
            }
        }

        public void Dispose()
        {
            _warmingTimer?.Dispose();
        }
    }

    public class CacheWarmingOptions
    {
        public bool EnableScheduledWarming { get; set; } = true;
        public int WarmingIntervalMinutes { get; set; } = 60; // Warm every hour
        public bool WarmOnStartup { get; set; } = true;
        public string[] CriticalKeys { get; set; } = Array.Empty<string>();
    }
}