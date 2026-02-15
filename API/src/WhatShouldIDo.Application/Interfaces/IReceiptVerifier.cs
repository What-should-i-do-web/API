using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Provider-agnostic interface for verifying subscription receipts
    /// </summary>
    public interface IReceiptVerifier
    {
        /// <summary>
        /// Verifies a receipt with the appropriate provider
        /// </summary>
        /// <param name="request">The verification request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The verification result</returns>
        Task<ReceiptVerificationResult> VerifyAsync(
            VerifyReceiptRequest request,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of receipt verification from a provider
    /// </summary>
    public class ReceiptVerificationResult
    {
        /// <summary>
        /// Whether verification was successful
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Whether verification is disabled (returns 501)
        /// </summary>
        public bool IsDisabled { get; set; }

        /// <summary>
        /// Error code if verification failed
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Error message if verification failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The provider that verified this receipt
        /// </summary>
        public SubscriptionProvider Provider { get; set; }

        /// <summary>
        /// The plan from the receipt
        /// </summary>
        public SubscriptionPlan Plan { get; set; }

        /// <summary>
        /// The status determined from the receipt
        /// </summary>
        public SubscriptionStatus Status { get; set; }

        /// <summary>
        /// When the trial ends (if trialing)
        /// </summary>
        public DateTime? TrialEndsAtUtc { get; set; }

        /// <summary>
        /// When the current billing period ends
        /// </summary>
        public DateTime? CurrentPeriodEndsAtUtc { get; set; }

        /// <summary>
        /// External subscription ID from the provider
        /// </summary>
        public string? ExternalSubscriptionId { get; set; }

        /// <summary>
        /// Whether auto-renewal is enabled
        /// </summary>
        public bool AutoRenew { get; set; }

        /// <summary>
        /// Creates a disabled result
        /// </summary>
        public static ReceiptVerificationResult Disabled()
        {
            return new ReceiptVerificationResult
            {
                IsValid = false,
                IsDisabled = true,
                ErrorCode = "VERIFICATION_DISABLED",
                ErrorMessage = "Subscription verification is disabled on this environment."
            };
        }

        /// <summary>
        /// Creates a validation failure result
        /// </summary>
        public static ReceiptVerificationResult Invalid(string errorCode, string errorMessage)
        {
            return new ReceiptVerificationResult
            {
                IsValid = false,
                IsDisabled = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Creates a successful verification result
        /// </summary>
        public static ReceiptVerificationResult Valid(
            SubscriptionProvider provider,
            SubscriptionPlan plan,
            SubscriptionStatus status,
            DateTime? currentPeriodEndsAtUtc,
            DateTime? trialEndsAtUtc = null,
            string? externalSubscriptionId = null,
            bool autoRenew = true)
        {
            return new ReceiptVerificationResult
            {
                IsValid = true,
                IsDisabled = false,
                Provider = provider,
                Plan = plan,
                Status = status,
                CurrentPeriodEndsAtUtc = currentPeriodEndsAtUtc,
                TrialEndsAtUtc = trialEndsAtUtc,
                ExternalSubscriptionId = externalSubscriptionId,
                AutoRenew = autoRenew
            };
        }
    }
}
