using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class RedisHealthChecker
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisHealthChecker> _logger;

        public RedisHealthChecker(IDistributedCache cache, ILogger<RedisHealthChecker> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<bool> TestAsync()
        {
            try
            {
                var key = "redis_test";
                var value = Guid.NewGuid().ToString();

                await _cache.SetStringAsync(key, value, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                });

                var readValue = await _cache.GetStringAsync(key);
                var success = readValue == value;

                _logger.LogInformation("Redis test başarılı mı: {success}", success);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis testi başarısız.");
                return false;
            }
        }
    }

}
