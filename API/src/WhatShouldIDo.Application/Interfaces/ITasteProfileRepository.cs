using System;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Repository for managing UserTasteProfile persistence.
    /// Handles optimistic concurrency with RowVersion.
    /// </summary>
    public interface ITasteProfileRepository
    {
        /// <summary>
        /// Get taste profile by user ID.
        /// Returns null if user has no profile.
        /// </summary>
        Task<UserTasteProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get taste profile by ID.
        /// </summary>
        Task<UserTasteProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create new taste profile.
        /// </summary>
        Task<UserTasteProfile> CreateAsync(UserTasteProfile profile, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update existing taste profile.
        /// Uses optimistic concurrency control with RowVersion.
        /// Throws DbUpdateConcurrencyException if profile was modified by another request.
        /// </summary>
        Task<UserTasteProfile> UpdateAsync(UserTasteProfile profile, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete taste profile.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if user has a taste profile.
        /// </summary>
        Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
