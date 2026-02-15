using System;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Redis-backed store for anonymous taste quiz drafts.
    /// Drafts expire after configured TTL (default 24 hours).
    /// </summary>
    public interface ITasteDraftStore
    {
        /// <summary>
        /// Save a draft taste profile with a claim token.
        /// Token is hashed (SHA256) before storage for security.
        /// </summary>
        /// <param name="claimToken">Random claim token (not hashed).</param>
        /// <param name="profile">Draft taste profile to store.</param>
        /// <param name="ttl">Time-to-live for the draft.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveDraftAsync(
            string claimToken,
            UserTasteProfile profile,
            TimeSpan ttl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieve a draft taste profile by claim token.
        /// Returns null if token is invalid or expired.
        /// </summary>
        /// <param name="claimToken">Claim token (will be hashed for lookup).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<UserTasteProfile?> GetDraftAsync(
            string claimToken,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a draft after successful claim.
        /// </summary>
        /// <param name="claimToken">Claim token (will be hashed for lookup).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteDraftAsync(
            string claimToken,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a draft exists for the given token.
        /// </summary>
        Task<bool> ExistsAsync(
            string claimToken,
            CancellationToken cancellationToken = default);
    }
}
