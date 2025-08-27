using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace WhatShouldIDo.Infrastructure.Caching
{
    public class InMemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<InMemoryCacheService> _logger;

        public InMemoryCacheService(IMemoryCache cache, ILogger<InMemoryCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> acquire, TimeSpan? absoluteExpiration = null)
        {
            if (_cache.TryGetValue(key, out T cachedResult))
            {
                _logger.LogInformation("Memory cache hit: {key}", key);
                return cachedResult;
            }

            var result = await acquire();
            
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpiration ?? TimeSpan.FromMinutes(30)
            };

            _cache.Set(key, result, options);
            _logger.LogInformation("💾 Memory cache set: {key} (TTL: {ttl} min)", key, options.AbsoluteExpirationRelativeToNow?.TotalMinutes);

            return result;
        }

        public async Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            _logger.LogWarning("Memory cache removed: {key}", key);
            await Task.CompletedTask;
        }
    }
}