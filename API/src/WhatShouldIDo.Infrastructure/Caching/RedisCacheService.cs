using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Caching
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheService> _logger;

        public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> acquire, TimeSpan? absoluteExpiration = null)
        {
            var cached = await _cache.GetStringAsync(key);
            if (cached != null)
            {
                _logger.LogInformation("Redis hit: {key}", key);
                return JsonSerializer.Deserialize<T>(cached);
            }

            var result = await acquire();
            var json = JsonSerializer.Serialize(result);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpiration ?? TimeSpan.FromMinutes(30)
            };

            await _cache.SetStringAsync(key, json, options);
            _logger.LogInformation("💾 Redis set: {key} (TTL: {ttl} min)", key, options.AbsoluteExpirationRelativeToNow?.TotalMinutes);

            return result;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            var cached = await _cache.GetStringAsync(key);
            if (cached != null)
            {
                return JsonSerializer.Deserialize<T>(cached);
            }
            return null;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };
            await _cache.SetStringAsync(key, json, options);
        }

        public async Task<bool> ExistsAsync(string key)
        {
            var cached = await _cache.GetStringAsync(key);
            return cached != null;
        }

        public async Task RemoveAsync(string key)
        {
            await _cache.RemoveAsync(key);
            _logger.LogWarning("Redis removed: {key}", key);
        }
    }
}
