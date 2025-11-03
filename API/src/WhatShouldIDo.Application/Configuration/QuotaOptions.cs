using System;
using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.Configuration
{
    /// <summary>
    /// Configuration options for user quota management.
    /// </summary>
    public class QuotaOptions
    {
        /// <summary>
        /// The default number of free requests granted to non-premium users.
        /// </summary>
        [Range(1, 1000)]
        public int DefaultFreeQuota { get; set; } = 5;

        /// <summary>
        /// Indicates whether daily quota reset is enabled.
        /// When enabled, user quotas reset at the specified UTC time each day.
        /// </summary>
        public bool DailyResetEnabled { get; set; } = false;

        /// <summary>
        /// The UTC time at which daily quota resets occur (if enabled).
        /// Specified as TimeSpan from midnight (e.g., 02:00:00 for 2 AM UTC).
        /// </summary>
        public TimeSpan DailyResetAtUtc { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Storage backend to use for quota persistence.
        /// Valid values: "InMemory", "Redis", "Database"
        /// </summary>
        [Required]
        public string StorageBackend { get; set; } = "InMemory";
    }
}
