using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Quota
{
    /// <summary>
    /// In-memory implementation of quota storage using thread-safe concurrent dictionary.
    /// Suitable for development and testing environments.
    /// Data is lost on application restart.
    /// </summary>
    public class InMemoryQuotaStore : IQuotaStore
    {
        private readonly ConcurrentDictionary<Guid, int> _quotaStore = new();
        private readonly ILogger<InMemoryQuotaStore> _logger;

        public InMemoryQuotaStore(ILogger<InMemoryQuotaStore> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<int?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (_quotaStore.TryGetValue(userId, out var quota))
            {
                _logger.LogDebug("Retrieved quota for user {UserId}: {Quota}", userId, quota);
                return Task.FromResult<int?>(quota);
            }

            _logger.LogDebug("No quota found for user {UserId}", userId);
            return Task.FromResult<int?>(null);
        }

        /// <inheritdoc />
        public Task<bool> CompareExchangeConsumeAsync(Guid userId, int amount, CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Amount must be positive", nameof(amount));
            }

            // Atomic compare-exchange loop
            bool success = false;
            _quotaStore.AddOrUpdate(
                userId,
                addValueFactory: _ =>
                {
                    // If key doesn't exist, cannot consume
                    return 0;
                },
                updateValueFactory: (_, current) =>
                {
                    if (current >= amount)
                    {
                        success = true;
                        return current - amount;
                    }
                    // Insufficient quota, return unchanged
                    return current;
                });

            if (success)
            {
                _logger.LogInformation("Consumed {Amount} credits for user {UserId}", amount, userId);
            }
            else
            {
                _logger.LogWarning("Failed to consume {Amount} credits for user {UserId} - insufficient quota", amount, userId);
            }

            return Task.FromResult(success);
        }

        /// <inheritdoc />
        public Task SetAsync(Guid userId, int value, CancellationToken cancellationToken = default)
        {
            if (value < 0)
            {
                throw new ArgumentException("Quota value cannot be negative", nameof(value));
            }

            _quotaStore[userId] = value;
            _logger.LogInformation("Set quota for user {UserId} to {Quota}", userId, value);
            return Task.CompletedTask;
        }
    }
}
