using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Repository for managing UserTasteEvent persistence.
    /// Events are append-only for audit trail and analytics.
    /// </summary>
    public interface ITasteEventRepository
    {
        /// <summary>
        /// Add a new taste event (append-only).
        /// </summary>
        Task AddAsync(UserTasteEvent tasteEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get recent taste events for a user (paginated).
        /// </summary>
        Task<List<UserTasteEvent>> GetByUserIdAsync(
            Guid userId,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get taste events by type for a user.
        /// </summary>
        Task<List<UserTasteEvent>> GetByUserIdAndTypeAsync(
            Guid userId,
            string eventType,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get taste events within a time range.
        /// </summary>
        Task<List<UserTasteEvent>> GetByUserIdAndTimeRangeAsync(
            Guid userId,
            DateTime startUtc,
            DateTime endUtc,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Count total events for a user.
        /// </summary>
        Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
