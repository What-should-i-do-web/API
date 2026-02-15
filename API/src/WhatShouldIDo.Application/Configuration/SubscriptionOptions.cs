using System.ComponentModel.DataAnnotations;

namespace WhatShouldIDo.Application.Configuration
{
    /// <summary>
    /// Configuration options for subscription verification
    /// </summary>
    public class SubscriptionOptions
    {
        /// <summary>
        /// Whether receipt verification is enabled.
        /// When false, verify endpoint returns 501 Not Implemented.
        /// Default: false (safe for web-only deployment)
        /// </summary>
        public bool VerificationEnabled { get; set; } = false;

        /// <summary>
        /// Whether to allow dev/test receipts (TEST_MONTHLY, TEST_YEARLY).
        /// Should only be true in Development environment.
        /// Default: false
        /// </summary>
        public bool AllowDevTestReceipts { get; set; } = false;

        /// <summary>
        /// Apple App Store Connect shared secret for server-side verification.
        /// Not required until mobile integration.
        /// </summary>
        public string? AppleSharedSecret { get; set; }

        /// <summary>
        /// Google Play service account JSON for server-side verification.
        /// Not required until mobile integration.
        /// </summary>
        public string? GoogleServiceAccountJson { get; set; }

        /// <summary>
        /// Default trial duration for monthly subscriptions in days.
        /// </summary>
        [Range(0, 30)]
        public int MonthlyTrialDays { get; set; } = 7;

        /// <summary>
        /// Default trial duration for yearly subscriptions in days.
        /// </summary>
        [Range(0, 60)]
        public int YearlyTrialDays { get; set; } = 30;

        /// <summary>
        /// How often to re-verify subscriptions with providers (in hours).
        /// 0 means no automatic re-verification.
        /// </summary>
        [Range(0, 168)]
        public int ReverificationIntervalHours { get; set; } = 24;

        /// <summary>
        /// Grace period after expiration before revoking access (in hours).
        /// </summary>
        [Range(0, 72)]
        public int GracePeriodHours { get; set; } = 24;
    }
}
