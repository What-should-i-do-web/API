using System;
using System.Threading;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Low-level storage abstraction for quota state.
    /// Implementations must provide thread-safe, atomic operations.
    /// </summary>
    public interface IQuotaStore
    {
        /// <summary>
        /// Retrieves the current quota value for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current quota value, or null if not set.</returns>
        Task<int?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically decrements quota if sufficient credits are available.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="amount">The number of credits to consume.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the quota was decremented; false if insufficient quota.</returns>
        Task<bool> CompareExchangeConsumeAsync(Guid userId, int amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the quota value for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="value">The quota value to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetAsync(Guid userId, int value, CancellationToken cancellationToken = default);
    }
}
