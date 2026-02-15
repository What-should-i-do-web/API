using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Repository for managing user subscriptions
    /// </summary>
    public interface ISubscriptionRepository
    {
        /// <summary>
        /// Gets a user's subscription by user ID
        /// </summary>
        Task<UserSubscription?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new subscription
        /// </summary>
        Task<UserSubscription> CreateAsync(UserSubscription subscription, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing subscription
        /// </summary>
        Task<UserSubscription> UpdateAsync(UserSubscription subscription, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates or updates a subscription (upsert by userId)
        /// </summary>
        Task<UserSubscription> UpsertAsync(UserSubscription subscription, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a user has a subscription record
        /// </summary>
        Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
