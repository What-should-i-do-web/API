using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;

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

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            _cache.TryGetValue(key, out T? cachedResult);
            if (cachedResult != null)
            {
                _logger.LogInformation("Memory cache hit: {key}", key);
            }
            return Task.FromResult(cachedResult);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };
            _cache.Set(key, value, options);
            _logger.LogInformation("💾 Memory cache set: {key} (TTL: {ttl} min)", key, expiration.TotalMinutes);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key)
        {
            bool exists = _cache.TryGetValue(key, out _);
            return Task.FromResult(exists);
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            _logger.LogWarning("Memory cache removed: {key}", key);
            return Task.CompletedTask;
        }
    }
}