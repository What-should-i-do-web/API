using System;
using System.Threading;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for managing user quota consumption and limits.
    /// </summary>
    public interface IQuotaService
    {
        /// <summary>
        /// Gets the remaining quota credits for a user.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of remaining credits.</returns>
        Task<int> GetRemainingAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to consume quota credits atomically.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="amount">The number of credits to consume.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if credits were successfully consumed; false if insufficient quota.</returns>
        Task<bool> TryConsumeAsync(Guid userId, int amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Initializes quota for a user if not already set. Seeds default quota for non-premium users.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InitializeIfNeededAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
