using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

        public  async Task RemoveAsync(string key)
        {
            await _cache.RemoveAsync(key);
            _logger.LogWarning("Redis removed: {key}", key);
        }
    }
}
