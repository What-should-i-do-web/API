using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing user subscriptions
    /// </summary>
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly WhatShouldIDoDbContext _context;
        private readonly ILogger<SubscriptionRepository> _logger;

        public SubscriptionRepository(WhatShouldIDoDbContext context, ILogger<SubscriptionRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserSubscription?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.UserSubscriptions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subscription for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<UserSubscription> CreateAsync(UserSubscription subscription, CancellationToken cancellationToken = default)
        {
            try
            {
                subscription.CreatedAtUtc = DateTime.UtcNow;
                subscription.UpdatedAtUtc = DateTime.UtcNow;

                _context.UserSubscriptions.Add(subscription);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Subscription created for user {UserId}: Plan={Plan}, Status={Status}",
                    subscription.UserId, subscription.Plan, subscription.Status);

                return subscription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subscription for user: {UserId}", subscription.UserId);
                throw;
            }
        }

        public async Task<UserSubscription> UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken = default)
        {
            try
            {
                subscription.UpdatedAtUtc = DateTime.UtcNow;

                _context.UserSubscriptions.Update(subscription);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Subscription updated for user {UserId}: Plan={Plan}, Status={Status}",
                    subscription.UserId, subscription.Plan, subscription.Status);

                return subscription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating subscription for user: {UserId}", subscription.UserId);
                throw;
            }
        }

        public async Task<UserSubscription> UpsertAsync(UserSubscription subscription, CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await _context.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == subscription.UserId, cancellationToken);

                if (existing == null)
                {
                    return await CreateAsync(subscription, cancellationToken);
                }

                // Update existing record with new values
                existing.Provider = subscription.Provider;
                existing.Plan = subscription.Plan;
                existing.Status = subscription.Status;
                existing.TrialEndsAtUtc = subscription.TrialEndsAtUtc;
                existing.CurrentPeriodEndsAtUtc = subscription.CurrentPeriodEndsAtUtc;
                existing.AutoRenew = subscription.AutoRenew;
                existing.ExternalSubscriptionId = subscription.ExternalSubscriptionId;
                existing.LastVerifiedAtUtc = subscription.LastVerifiedAtUtc;
                existing.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Subscription upserted for user {UserId}: Plan={Plan}, Status={Status}",
                    existing.UserId, existing.Plan, existing.Status);

                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting subscription for user: {UserId}", subscription.UserId);
                throw;
            }
        }

        public async Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.UserSubscriptions
                    .AsNoTracking()
                    .AnyAsync(s => s.UserId == userId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking subscription existence for user: {UserId}", userId);
                throw;
            }
        }
    }
}
