using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using WhatShouldIDo.Infrastructure.Options;

namespace WhatShouldIDo.Infrastructure.Caching
{
    public interface ICacheInvalidationService
    {
        Task InvalidatePatternAsync(string pattern);
        Task InvalidateTagAsync(string tag);
        Task WarmCacheAsync(string key, Func<Task<object>> dataFactory, TimeSpan? expiration = null);
        Task<CacheStatistics> GetStatisticsAsync();
    }

    public class RedisClusterCacheService : ICacheService, ICacheInvalidationService
    {
        private readonly IDatabase _database;
        private readonly IServer _server;
        private readonly ILogger<RedisClusterCacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly CacheStatistics _statistics;

        public RedisClusterCacheService(
            IConnectionMultiplexer redis, 
            ILogger<RedisClusterCacheService> logger,
            IOptions<CacheOptions> cacheOptions)
        {
            _database = redis.GetDatabase();
            _server = redis.GetServer(redis.GetEndPoints().First());
            _logger = logger;
            _statistics = new CacheStatistics();
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> acquire, TimeSpan? absoluteExpiration = null)
        {
            var cacheKey = FormatKey(key);
            
            try
            {
                // Try to get from cache
                var cachedValue = await _database.StringGetAsync(cacheKey);
                if (cachedValue.HasValue)
                {
                    _statistics.RecordHit();
                    _logger.LogDebug("🔥 Cache HIT: {Key}", key);
                    
                    var deserializedValue = JsonSerializer.Deserialize<T>(cachedValue!, _jsonOptions);
                    return deserializedValue!;
                }

                _statistics.RecordMiss();
                _logger.LogDebug("❄️ Cache MISS: {Key}", key);

                // Get fresh data
                var freshValue = await acquire();
                if (freshValue != null)
                {
                    // Cache the result
                    var serializedValue = JsonSerializer.Serialize(freshValue, _jsonOptions);
                    var expiry = absoluteExpiration ?? TimeSpan.FromMinutes(30);
                    
                    await _database.StringSetAsync(cacheKey, serializedValue, expiry);
                    await AddCacheTag(cacheKey, GetTagFromKey(key));
                    
                    _logger.LogInformation("💾 Cache SET: {Key} (TTL: {TTL} min)", key, expiry.TotalMinutes);
                }

                return freshValue;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis cluster error for key {Key}. Executing without cache.", key);
                _statistics.RecordError();
                return await acquire();
            }
        }

        public async Task RemoveAsync(string key)
        {
            var cacheKey = FormatKey(key);
            
            try
            {
                var deleted = await _database.KeyDeleteAsync(cacheKey);
                if (deleted)
                {
                    await RemoveCacheTag(cacheKey);
                    _logger.LogInformation("🗑️ Cache REMOVE: {Key}", key);
                }
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Failed to remove key {Key} from Redis cluster", key);
                _statistics.RecordError();
            }
        }

        // Cache invalidation by pattern
        public async Task InvalidatePatternAsync(string pattern)
        {
            try
            {
                var keys = _server.Keys(pattern: FormatKey(pattern));
                var tasks = keys.Select(key => _database.KeyDeleteAsync(key));
                var deletedCount = await Task.WhenAll(tasks);
                
                _logger.LogInformation("🧹 Cache PATTERN INVALIDATE: {Pattern} ({Count} keys)", pattern, deletedCount.Count(r => r));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate cache pattern {Pattern}", pattern);
                _statistics.RecordError();
            }
        }

        // Cache invalidation by tag
        public async Task InvalidateTagAsync(string tag)
        {
            try
            {
                var tagKey = $"tag:{tag}";
                var taggedKeys = await _database.SetMembersAsync(tagKey);
                
                if (taggedKeys.Length > 0)
                {
                    var tasks = taggedKeys.Select(key => _database.KeyDeleteAsync(key.ToString()));
                    var deletedCount = await Task.WhenAll(tasks);
                    
                    // Remove the tag set itself
                    await _database.KeyDeleteAsync(tagKey);
                    
                    _logger.LogInformation("🏷️ Cache TAG INVALIDATE: {Tag} ({Count} keys)", tag, deletedCount.Count(r => r));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate cache tag {Tag}", tag);
                _statistics.RecordError();
            }
        }

        // Cache warming
        public async Task WarmCacheAsync(string key, Func<Task<object>> dataFactory, TimeSpan? expiration = null)
        {
            try
            {
                var cacheKey = FormatKey(key);
                var exists = await _database.KeyExistsAsync(cacheKey);
                
                if (!exists)
                {
                    _logger.LogInformation("🔥 Cache WARMING: {Key}", key);
                    
                    var data = await dataFactory();
                    if (data != null)
                    {
                        var serializedValue = JsonSerializer.Serialize(data, _jsonOptions);
                        var expiry = expiration ?? TimeSpan.FromHours(1);
                        
                        await _database.StringSetAsync(cacheKey, serializedValue, expiry);
                        await AddCacheTag(cacheKey, GetTagFromKey(key));
                        
                        _logger.LogInformation("💾 Cache WARMED: {Key} (TTL: {TTL} min)", key, expiry.TotalMinutes);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to warm cache for key {Key}", key);
                _statistics.RecordError();
            }
        }

        public async Task<CacheStatistics> GetStatisticsAsync()
        {
            try
            {
                var info = await _database.ExecuteAsync("INFO", "stats");
                var infoString = info.ToString();
                
                // Parse Redis stats
                if (infoString.Contains("keyspace_hits:"))
                {
                    var lines = infoString.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("keyspace_hits:"))
                        {
                            if (long.TryParse(line.Split(':')[1].Trim(), out var hits))
                                _statistics.TotalHits = hits;
                        }
                        else if (line.StartsWith("keyspace_misses:"))
                        {
                            if (long.TryParse(line.Split(':')[1].Trim(), out var misses))
                                _statistics.TotalMisses = misses;
                        }
                    }
                }
                
                return _statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get cache statistics");
                return _statistics;
            }
        }

        // Helper methods
        private static string FormatKey(string key) => $"whatshouldi:{key}";
        
        private static string GetTagFromKey(string key)
        {
            // Extract tag from key pattern (e.g., "places:nearby" -> "places")
            var parts = key.Split(':');
            return parts.Length > 0 ? parts[0] : "general";
        }

        private async Task AddCacheTag(string cacheKey, string tag)
        {
            try
            {
                await _database.SetAddAsync($"tag:{tag}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add cache tag {Tag} for key {Key}", tag, cacheKey);
            }
        }

        private async Task RemoveCacheTag(string cacheKey)
        {
            try
            {
                // This is simplified - in production, you'd maintain tag relationships
                var tag = GetTagFromKey(cacheKey.Replace("whatshouldi:", ""));
                await _database.SetRemoveAsync($"tag:{tag}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove cache tag for key {Key}", cacheKey);
            }
        }
    }

    // Cache statistics class
    public class CacheStatistics
    {
        private long _totalHits;
        private long _totalMisses;
        private long _totalErrors;

        public long TotalHits 
        { 
            get => _totalHits; 
            set => _totalHits = value; 
        }
        
        public long TotalMisses 
        { 
            get => _totalMisses; 
            set => _totalMisses = value; 
        }
        
        public long TotalErrors 
        { 
            get => _totalErrors; 
            set => _totalErrors = value; 
        }

        public double HitRate => TotalHits + TotalMisses > 0 ? (double)TotalHits / (TotalHits + TotalMisses) : 0;
        public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;

        public void RecordHit() 
        { 
            Interlocked.Increment(ref _totalHits);
            LastUpdated = DateTime.UtcNow;
        }
        
        public void RecordMiss() 
        { 
            Interlocked.Increment(ref _totalMisses);
            LastUpdated = DateTime.UtcNow;
        }
        
        public void RecordError() 
        { 
            Interlocked.Increment(ref _totalErrors);
            LastUpdated = DateTime.UtcNow;
        }
    }

    // Cache options
    public class CacheOptions
    {
        public int DefaultTtlMinutes { get; set; } = 30;
        public int MaxTtlMinutes { get; set; } = 60;
        public bool EnableCacheWarming { get; set; } = true;
        public string[] WarmingKeys { get; set; } = Array.Empty<string>();
    }
}