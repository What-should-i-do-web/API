using System.ComponentModel.DataAnnotations;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    /// <summary>
    /// Request to manually grant a subscription (admin-only).
    /// Used for internal grants, compensation, or testing.
    /// </summary>
    public class ManualGrantRequest
    {
        /// <summary>
        /// The user ID to grant the subscription to
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// The subscription plan to grant (Monthly or Yearly, not Free)
        /// </summary>
        [Required]
        public SubscriptionPlan Plan { get; set; }

        /// <summary>
        /// When the grant expires (must be in the future)
        /// </summary>
        [Required]
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>
        /// Reason for the grant (required for audit trail).
        /// Should NOT contain PII. Max 500 characters.
        /// Examples: "Beta tester reward", "Support compensation #12345"
        /// </summary>
        [Required]
        [MinLength(5)]
        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
    }
}
