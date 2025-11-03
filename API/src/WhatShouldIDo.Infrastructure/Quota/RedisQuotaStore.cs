using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Quota
{
    /// <summary>
    /// Redis-based implementation of quota storage with atomic operations.
    /// Suitable for production environments with distributed deployment.
    /// </summary>
    public class RedisQuotaStore : IQuotaStore
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisQuotaStore> _logger;
        private const string KeyPrefix = "quota:";

        // Lua script for atomic compare-exchange-consume operation
        private static readonly string CompareExchangeScript = @"
            local key = KEYS[1]
            local amount = tonumber(ARGV[1])
            local current = tonumber(redis.call('GET', key))

            if current == nil then
                return 0
            end

            if current >= amount then
                redis.call('DECRBY', key, amount)
                return 1
            else
                return 0
            end
        ";

        public RedisQuotaStore(IConnectionMultiplexer redis, ILogger<RedisQuotaStore> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<int?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = GetKey(userId);
                var value = await db.StringGetAsync(key);

                if (value.IsNullOrEmpty)
                {
                    _logger.LogDebug("No quota found for user {UserId} in Redis", userId);
                    return null;
                }

                var quota = (int)value;
                _logger.LogDebug("Retrieved quota for user {UserId}: {Quota}", userId, quota);
                return quota;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quota from Redis for user {UserId}", userId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> CompareExchangeConsumeAsync(Guid userId, int amount, CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Amount must be positive", nameof(amount));
            }

            try
            {
                var db = _redis.GetDatabase();
                var key = GetKey(userId);

                // Execute atomic Lua script
                var result = await db.ScriptEvaluateAsync(
                    CompareExchangeScript,
                    new RedisKey[] { key },
                    new RedisValue[] { amount }
                );

                bool success = (int)result == 1;

                if (success)
                {
                    _logger.LogInformation("Consumed {Amount} credits for user {UserId} in Redis", amount, userId);
                }
                else
                {
                    _logger.LogWarning("Failed to consume {Amount} credits for user {UserId} in Redis - insufficient quota", amount, userId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming quota in Redis for user {UserId}", userId);
                // Fail closed - deny consumption on error
                return false;
            }
        }

        /// <inheritdoc />
        public async Task SetAsync(Guid userId, int value, CancellationToken cancellationToken = default)
        {
            if (value < 0)
            {
                throw new ArgumentException("Quota value cannot be negative", nameof(value));
            }

            try
            {
                var db = _redis.GetDatabase();
                var key = GetKey(userId);
                await db.StringSetAsync(key, value);
                _logger.LogInformation("Set quota for user {UserId} to {Quota} in Redis", userId, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting quota in Redis for user {UserId}", userId);
                throw;
            }
        }

        private static string GetKey(Guid userId) => $"{KeyPrefix}{userId}";
    }
}
