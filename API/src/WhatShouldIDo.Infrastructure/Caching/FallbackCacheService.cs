using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace WhatShouldIDo.Infrastructure.Caching
{
    public class FallbackCacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<FallbackCacheService> _logger;
        private volatile bool _redisAvailable = true;

        public FallbackCacheService(IDistributedCache distributedCache, IMemoryCache memoryCache, ILogger<FallbackCacheService> logger)
        {
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> acquire, TimeSpan? absoluteExpiration = null)
        {
            // Try Redis first if available
            if (_redisAvailable)
            {
                try
                {
                    var cached = await _distributedCache.GetStringAsync(key);
                    if (cached != null)
                    {
                        _logger.LogInformation("Redis cache hit: {key}", key);
                        return JsonSerializer.Deserialize<T>(cached);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Redis unavailable, falling back to memory cache");
                    _redisAvailable = false;
                }
            }

            // Try memory cache if Redis failed
            if (_memoryCache.TryGetValue(key, out T cachedResult))
            {
                _logger.LogInformation("Memory cache hit: {key}", key);
                return cachedResult;
            }

            // Get fresh data
            var result = await acquire();
            var ttl = absoluteExpiration ?? TimeSpan.FromMinutes(30);

            // Store in both caches (Redis if available, memory as backup)
            if (_redisAvailable)
            {
                try
                {
                    var json = JsonSerializer.Serialize(result);
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ttl
                    };
                    await _distributedCache.SetStringAsync(key, json, options);
                    _logger.LogInformation("💾 Redis cache set: {key} (TTL: {ttl} min)", key, ttl.TotalMinutes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Redis set failed, using memory cache only");
                    _redisAvailable = false;
                }
            }

            // Always store in memory cache as backup
            var memOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };
            _memoryCache.Set(key, result, memOptions);
            _logger.LogInformation("💾 Memory cache set: {key} (TTL: {ttl} min)", key, ttl.TotalMinutes);

            return result;
        }

        public async Task RemoveAsync(string key)
        {
            if (_redisAvailable)
            {
                try
                {
                    await _distributedCache.RemoveAsync(key);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Redis remove failed");
                    _redisAvailable = false;
                }
            }

            _memoryCache.Remove(key);
            _logger.LogWarning("Cache removed: {key}", key);
        }
    }
}