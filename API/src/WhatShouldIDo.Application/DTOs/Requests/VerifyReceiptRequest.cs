using System.ComponentModel.DataAnnotations;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request to verify a subscription receipt from Apple or Google
    /// </summary>
    public class VerifyReceiptRequest
    {
        /// <summary>
        /// The provider of the receipt (Apple or Google)
        /// </summary>
        [Required]
        public SubscriptionProvider Provider { get; set; }

        /// <summary>
        /// The subscription plan being verified
        /// </summary>
        [Required]
        public SubscriptionPlan Plan { get; set; }

        /// <summary>
        /// The receipt data from the store.
        /// For Apple: the base64-encoded receipt
        /// For Google: the purchase token
        /// For test: "TEST_MONTHLY" or "TEST_YEARLY"
        /// SECURITY: Never log raw receipt data - use hash only.
        /// </summary>
        [Required]
        [MinLength(1)]
        public string ReceiptData { get; set; } = string.Empty;

        /// <summary>
        /// Whether the user is requesting a trial (if eligible)
        /// </summary>
        public bool IsTrialRequested { get; set; } = false;
    }
}
