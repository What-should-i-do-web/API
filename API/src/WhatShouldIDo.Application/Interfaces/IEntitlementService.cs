using System;
using System.Threading;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for determining user entitlements and subscription status.
    /// </summary>
    public interface IEntitlementService
    {
        /// <summary>
        /// Determines if a user has premium subscription status.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the user has premium access; otherwise, false.</returns>
        Task<bool> IsPremiumAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
