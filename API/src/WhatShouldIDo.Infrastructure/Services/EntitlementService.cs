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
    /// Reads from JWT claims first, with optional fallback to repository.
    /// </summary>
    public class EntitlementService : IEntitlementService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<EntitlementService> _logger;

        // Known claim types for subscription status
        private const string SubscriptionClaimType = "subscription";
        private const string RoleClaimType = ClaimTypes.Role;
        private const string PremiumValue = "premium";

        public EntitlementService(
            IHttpContextAccessor httpContextAccessor,
            ILogger<EntitlementService> logger)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task<bool> IsPremiumAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user == null || !user.Identity?.IsAuthenticated == true)
                {
                    _logger.LogDebug("User {UserId} not authenticated, defaulting to non-premium", userId);
                    return Task.FromResult(false);
                }

                // Check subscription claim
                var subscriptionClaim = user.FindFirst(SubscriptionClaimType);
                if (subscriptionClaim != null)
                {
                    bool isPremium = string.Equals(subscriptionClaim.Value, PremiumValue, StringComparison.OrdinalIgnoreCase);
                    _logger.LogDebug("User {UserId} subscription claim found: {Subscription}, isPremium: {IsPremium}",
                        userId, subscriptionClaim.Value, isPremium);
                    return Task.FromResult(isPremium);
                }

                // Check role claim as fallback
                var roleClaims = user.FindAll(RoleClaimType).Select(c => c.Value).ToList();
                if (roleClaims.Any())
                {
                    bool isPremium = roleClaims.Any(r => string.Equals(r, PremiumValue, StringComparison.OrdinalIgnoreCase));
                    _logger.LogDebug("User {UserId} role claims found: [{Roles}], isPremium: {IsPremium}",
                        userId, string.Join(", ", roleClaims), isPremium);
                    return Task.FromResult(isPremium);
                }

                // No subscription or role claims found - default to non-premium
                _logger.LogDebug("User {UserId} has no subscription or role claims, defaulting to non-premium", userId);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining premium status for user {UserId}", userId);
                // Defensive: default to non-premium on error
                return Task.FromResult(false);
            }
        }
    }
}
