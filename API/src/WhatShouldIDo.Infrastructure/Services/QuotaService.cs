using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Service for managing user quota consumption and initialization.
    /// </summary>
    public class QuotaService : IQuotaService
    {
        private readonly IQuotaStore _quotaStore;
        private readonly IEntitlementService _entitlementService;
        private readonly QuotaOptions _options;
        private readonly ILogger<QuotaService> _logger;

        public QuotaService(
            IQuotaStore quotaStore,
            IEntitlementService entitlementService,
            IOptions<QuotaOptions> options,
            ILogger<QuotaService> logger)
        {
            _quotaStore = quotaStore ?? throw new ArgumentNullException(nameof(quotaStore));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<int> GetRemainingAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var remaining = await _quotaStore.GetAsync(userId, cancellationToken);

                if (remaining.HasValue)
                {
                    _logger.LogDebug("User {UserId} has {Remaining} credits remaining", userId, remaining.Value);
                    return remaining.Value;
                }

                // Quota not initialized yet
                _logger.LogDebug("User {UserId} quota not initialized", userId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quota for user {UserId}", userId);
                // Defensive: return 0 on error to fail closed
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<bool> TryConsumeAsync(Guid userId, int amount, CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
            {
                throw new ArgumentException("Amount must be positive", nameof(amount));
            }

            try
            {
                // Check if premium - they have unlimited quota
                var isPremium = await _entitlementService.IsPremiumAsync(userId, cancellationToken);
                if (isPremium)
                {
                    _logger.LogDebug("User {UserId} is premium, bypassing quota consumption", userId);
                    return true;
                }

                // Initialize quota if needed
                await InitializeIfNeededAsync(userId, cancellationToken);

                // Attempt atomic consumption
                var success = await _quotaStore.CompareExchangeConsumeAsync(userId, amount, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Successfully consumed {Amount} credits for user {UserId}", amount, userId);
                }
                else
                {
                    _logger.LogWarning("User {UserId} has insufficient quota to consume {Amount} credits", userId, amount);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming quota for user {UserId}", userId);
                // Defensive: fail closed on error
                return false;
            }
        }

        /// <inheritdoc />
        public async Task InitializeIfNeededAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await _quotaStore.GetAsync(userId, cancellationToken);

                if (existing.HasValue)
                {
                    _logger.LogDebug("Quota already initialized for user {UserId}", userId);
                    return;
                }

                // Check if premium
                var isPremium = await _entitlementService.IsPremiumAsync(userId, cancellationToken);

                if (isPremium)
                {
                    _logger.LogDebug("User {UserId} is premium, skipping quota initialization", userId);
                    return;
                }

                // Initialize with default free quota
                await _quotaStore.SetAsync(userId, _options.DefaultFreeQuota, cancellationToken);
                _logger.LogInformation("Initialized quota for user {UserId} with {Quota} credits",
                    userId, _options.DefaultFreeQuota);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing quota for user {UserId}", userId);
                throw;
            }
        }
    }
}
