using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Service for determining user entitlement and premium status.
    /// Checks database-backed subscription first, then JWT claims as fallback.
    /// This supports both mobile IAP subscriptions and manually granted premium access.
    /// </summary>
    public class EntitlementService : IEntitlementService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IClock _clock;
        private readonly ILogger<EntitlementService> _logger;

        // Known claim types for subscription status
        private const string SubscriptionClaimType = "subscription";
        private const string RoleClaimType = ClaimTypes.Role;
        private const string PremiumValue = "premium";

        public EntitlementService(
            IHttpContextAccessor httpContextAccessor,
            ISubscriptionRepository subscriptionRepository,
            IClock clock,
            ILogger<EntitlementService> logger)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<bool> IsPremiumAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. First check database-backed subscription (source of truth for IAP)
                var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
                if (subscription != null)
                {
                    var utcNow = _clock.UtcNow;
                    var hasEntitlement = subscription.HasEntitlementAt(utcNow);

                    _logger.LogDebug(
                        "User {UserId} subscription check: Status={Status}, Plan={Plan}, HasEntitlement={HasEntitlement}, PeriodEnd={PeriodEnd}",
                        userId, subscription.Status, subscription.Plan, hasEntitlement, subscription.CurrentPeriodEndsAtUtc);

                    if (hasEntitlement)
                    {
                        return true;
                    }
                }

                // 2. Fall back to JWT claims (for backward compatibility and manual grants)
                var user = _httpContextAccessor.HttpContext?.User;

                if (user == null || user.Identity?.IsAuthenticated != true)
                {
                    _logger.LogDebug("User {UserId} not authenticated and no DB subscription, defaulting to non-premium", userId);
                    return false;
                }

                // Check subscription claim
                var subscriptionClaim = user.FindFirst(SubscriptionClaimType);
                if (subscriptionClaim != null)
                {
                    bool isPremium = string.Equals(subscriptionClaim.Value, PremiumValue, StringComparison.OrdinalIgnoreCase);
                    _logger.LogDebug("User {UserId} subscription claim found: {Subscription}, isPremium: {IsPremium}",
                        userId, subscriptionClaim.Value, isPremium);
                    return isPremium;
                }

                // Check role claim as fallback
                var roleClaims = user.FindAll(RoleClaimType).Select(c => c.Value).ToList();
                if (roleClaims.Any())
                {
                    bool isPremium = roleClaims.Any(r => string.Equals(r, PremiumValue, StringComparison.OrdinalIgnoreCase));
                    _logger.LogDebug("User {UserId} role claims found: [{Roles}], isPremium: {IsPremium}",
                        userId, string.Join(", ", roleClaims), isPremium);
                    return isPremium;
                }

                // No subscription or role claims found - default to non-premium
                _logger.LogDebug("User {UserId} has no active subscription or premium claims, defaulting to non-premium", userId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining premium status for user {UserId}", userId);
                // Defensive: default to non-premium on error
                return false;
            }
        }
    }
}
