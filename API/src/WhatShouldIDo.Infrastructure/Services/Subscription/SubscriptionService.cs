using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Infrastructure.Services.Subscription
{
    /// <summary>
    /// Service for managing user subscriptions
    /// </summary>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IReceiptVerifier _receiptVerifier;
        private readonly SubscriptionOptions _options;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly IClock _clock;

        public SubscriptionService(
            ISubscriptionRepository subscriptionRepository,
            IReceiptVerifier receiptVerifier,
            IOptions<SubscriptionOptions> options,
            ILogger<SubscriptionService> logger,
            IClock clock)
        {
            _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
            _receiptVerifier = receiptVerifier ?? throw new ArgumentNullException(nameof(receiptVerifier));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<SubscriptionDto> GetMySubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var utcNow = _clock.UtcNow;
            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

            if (subscription == null)
            {
                // User has no subscription record - return default free tier
                _logger.LogDebug("No subscription found for user {UserId}, returning default free tier", userId);
                return CreateDefaultSubscriptionDto();
            }

            return MapToDto(subscription, utcNow);
        }

        public async Task<bool> UserHasPremiumEntitlementAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

            if (subscription == null)
            {
                return false;
            }

            var hasEntitlement = subscription.HasEntitlementAt(utcNow);

            _logger.LogDebug(
                "Entitlement check for user {UserId}: HasEntitlement={HasEntitlement}, Status={Status}, PeriodEnd={PeriodEnd}",
                userId, hasEntitlement, subscription.Status, subscription.CurrentPeriodEndsAtUtc);

            return hasEntitlement;
        }

        public async Task<VerifySubscriptionResultDto> VerifyReceiptAsync(
            Guid userId,
            VerifyReceiptRequest request,
            CancellationToken cancellationToken = default)
        {
            var utcNow = _clock.UtcNow;

            // Log receipt hash for audit (NEVER log raw receipt)
            var receiptHash = ComputeReceiptHash(request.ReceiptData);
            _logger.LogInformation(
                "Processing receipt verification for user {UserId}, provider {Provider}, receiptHash={ReceiptHash}",
                userId, request.Provider, receiptHash);

            // Verify with the receipt verifier (disabled, dev, or production)
            var verificationResult = await _receiptVerifier.VerifyAsync(request, cancellationToken);

            // If verification is disabled, return the disabled response
            if (verificationResult.IsDisabled)
            {
                _logger.LogInformation("Subscription verification is disabled for user {UserId}", userId);
                return VerifySubscriptionResultDto.Disabled();
            }

            // If verification failed, return error
            if (!verificationResult.IsValid)
            {
                _logger.LogWarning(
                    "Receipt verification failed for user {UserId}, receiptHash={ReceiptHash}: {ErrorCode} - {ErrorMessage}",
                    userId, receiptHash, verificationResult.ErrorCode, verificationResult.ErrorMessage);

                return VerifySubscriptionResultDto.Failed(
                    verificationResult.ErrorCode ?? "VERIFICATION_FAILED",
                    verificationResult.ErrorMessage ?? "Receipt verification failed.");
            }

            // Verification successful - update or create subscription
            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? UserSubscription.CreateDefault(userId, utcNow);

            // Update subscription based on verification result
            if (verificationResult.Status == SubscriptionStatus.Trialing)
            {
                subscription.StartTrial(
                    verificationResult.Provider,
                    verificationResult.Plan,
                    verificationResult.TrialEndsAtUtc ?? utcNow.AddDays(7),
                    utcNow,
                    verificationResult.ExternalSubscriptionId);
            }
            else
            {
                subscription.Activate(
                    verificationResult.Provider,
                    verificationResult.Plan,
                    verificationResult.CurrentPeriodEndsAtUtc ?? utcNow.AddMonths(1),
                    utcNow,
                    verificationResult.ExternalSubscriptionId,
                    verificationResult.AutoRenew);
            }

            // Persist the updated subscription
            subscription = await _subscriptionRepository.UpsertAsync(subscription, cancellationToken);

            _logger.LogInformation(
                "Subscription verified and updated for user {UserId}, receiptHash={ReceiptHash}: Plan={Plan}, Status={Status}",
                userId, receiptHash, subscription.Plan, subscription.Status);

            return VerifySubscriptionResultDto.Successful(MapToDto(subscription, utcNow));
        }

        public async Task EnsureSubscriptionExistsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var exists = await _subscriptionRepository.ExistsForUserAsync(userId, cancellationToken);

            if (!exists)
            {
                var utcNow = _clock.UtcNow;
                var defaultSubscription = UserSubscription.CreateDefault(userId, utcNow);
                await _subscriptionRepository.CreateAsync(defaultSubscription, cancellationToken);

                _logger.LogInformation("Created default subscription for new user {UserId}", userId);
            }
        }

        public async Task<SubscriptionDto> ManualGrantAsync(
            ManualGrantRequest request,
            Guid adminUserId,
            CancellationToken cancellationToken = default)
        {
            var utcNow = _clock.UtcNow;

            // Validate request
            if (request.Plan == SubscriptionPlan.Free)
            {
                throw new ArgumentException("Cannot manually grant Free plan. Use RevokeManualGrantAsync instead.", nameof(request));
            }

            if (request.ExpiresAtUtc <= utcNow)
            {
                throw new ArgumentException("ExpiresAtUtc must be in the future.", nameof(request));
            }

            // Get or create subscription
            var subscription = await _subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken)
                ?? UserSubscription.CreateDefault(request.UserId, utcNow);

            // Apply manual grant
            var notesWithAudit = $"[Admin:{adminUserId}] {request.Notes}";
            subscription.GrantManual(request.Plan, request.ExpiresAtUtc, utcNow, notesWithAudit);

            // Validate invariants before persisting
            subscription.EnsureInvariantsOrThrow();

            // Persist
            subscription = await _subscriptionRepository.UpsertAsync(subscription, cancellationToken);

            _logger.LogInformation(
                "Manual grant applied by admin {AdminUserId} for user {UserId}: Plan={Plan}, ExpiresAt={ExpiresAt}",
                adminUserId, request.UserId, subscription.Plan, subscription.CurrentPeriodEndsAtUtc);

            return MapToDto(subscription, utcNow);
        }

        public async Task<bool> RevokeManualGrantAsync(
            Guid userId,
            Guid adminUserId,
            CancellationToken cancellationToken = default)
        {
            var utcNow = _clock.UtcNow;

            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

            if (subscription == null)
            {
                _logger.LogWarning(
                    "Admin {AdminUserId} attempted to revoke grant for user {UserId}, but no subscription exists",
                    adminUserId, userId);
                return false;
            }

            // Only revoke if it's a manual grant
            if (subscription.Provider != SubscriptionProvider.Manual)
            {
                _logger.LogWarning(
                    "Admin {AdminUserId} attempted to revoke grant for user {UserId}, but provider is {Provider} (not Manual)",
                    adminUserId, userId, subscription.Provider);
                return false;
            }

            // Reset to free tier
            subscription.ResetToFree(utcNow);

            // Validate invariants before persisting
            subscription.EnsureInvariantsOrThrow();

            // Persist
            await _subscriptionRepository.UpsertAsync(subscription, cancellationToken);

            _logger.LogInformation(
                "Manual grant revoked by admin {AdminUserId} for user {UserId}",
                adminUserId, userId);

            return true;
        }

        private static SubscriptionDto CreateDefaultSubscriptionDto()
        {
            return new SubscriptionDto
            {
                Plan = SubscriptionPlan.Free,
                Status = SubscriptionStatus.None,
                Provider = SubscriptionProvider.None,
                TrialEndsAtUtc = null,
                CurrentPeriodEndsAtUtc = null,
                AutoRenew = false,
                HasEntitlement = false,
                EffectivePlan = SubscriptionPlan.Free
            };
        }

        private static SubscriptionDto MapToDto(UserSubscription subscription, DateTime utcNow)
        {
            return new SubscriptionDto
            {
                Plan = subscription.Plan,
                Status = subscription.Status,
                Provider = subscription.Provider,
                TrialEndsAtUtc = subscription.TrialEndsAtUtc,
                CurrentPeriodEndsAtUtc = subscription.CurrentPeriodEndsAtUtc,
                AutoRenew = subscription.AutoRenew,
                HasEntitlement = subscription.HasEntitlementAt(utcNow),
                EffectivePlan = subscription.EffectivePlanAt(utcNow)
            };
        }

        /// <summary>
        /// Computes a truncated SHA256 hash of the receipt for audit logging.
        /// SECURITY: Never log raw receipts - only first 8 chars of hash.
        /// </summary>
        private static string ComputeReceiptHash(string receiptData)
        {
            if (string.IsNullOrEmpty(receiptData))
                return "empty";

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(receiptData));
            var fullHash = Convert.ToHexString(bytes).ToLowerInvariant();
            return fullHash[..8]; // First 8 chars only
        }
    }
}
