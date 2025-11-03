using System;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Represents a user's quota allocation and consumption tracking.
    /// </summary>
    public class UserQuota
    {
        /// <summary>
        /// Gets or sets the unique identifier of the user.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the remaining quota credits available to the user.
        /// </summary>
        public int RemainingCredits { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last quota update.
        /// Used for daily reset calculations when enabled.
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the quota was first created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the row version for optimistic concurrency control.
        /// </summary>
        public byte[]? RowVersion { get; set; }
    }
}
