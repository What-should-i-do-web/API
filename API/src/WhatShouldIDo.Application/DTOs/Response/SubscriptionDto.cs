using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Application.DTOs.Response
{
    /// <summary>
    /// DTO representing the user's subscription state
    /// </summary>
    public class SubscriptionDto
    {
        /// <summary>
        /// The subscription plan (Free, Monthly, Yearly)
        /// </summary>
        public SubscriptionPlan Plan { get; set; }

        /// <summary>
        /// The current status of the subscription
        /// </summary>
        public SubscriptionStatus Status { get; set; }

        /// <summary>
        /// The provider where the subscription was purchased
        /// </summary>
        public SubscriptionProvider Provider { get; set; }

        /// <summary>
        /// When the trial period ends (if trialing)
        /// </summary>
        public DateTime? TrialEndsAtUtc { get; set; }

        /// <summary>
        /// When the current billing period ends
        /// </summary>
        public DateTime? CurrentPeriodEndsAtUtc { get; set; }

        /// <summary>
        /// Whether auto-renewal is enabled
        /// </summary>
        public bool AutoRenew { get; set; }

        /// <summary>
        /// Computed: whether the user currently has premium entitlement
        /// </summary>
        public bool HasEntitlement { get; set; }

        /// <summary>
        /// Computed: the effective plan considering current status
        /// </summary>
        public SubscriptionPlan EffectivePlan { get; set; }

        /// <summary>
        /// Display-friendly plan name
        /// </summary>
        public string PlanDisplayName => Plan switch
        {
            SubscriptionPlan.Free => "Free",
            SubscriptionPlan.Monthly => "Monthly Premium",
            SubscriptionPlan.Yearly => "Yearly Premium",
            _ => "Unknown"
        };

        /// <summary>
        /// Display-friendly status name
        /// </summary>
        public string StatusDisplayName => Status switch
        {
            SubscriptionStatus.None => "Free Tier",
            SubscriptionStatus.Trialing => "Trial",
            SubscriptionStatus.Active => "Active",
            SubscriptionStatus.PastDue => "Past Due",
            SubscriptionStatus.Canceled => "Canceled",
            SubscriptionStatus.Expired => "Expired",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Result of subscription verification
    /// </summary>
    public class VerifySubscriptionResultDto
    {
        /// <summary>
        /// Whether verification was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error code if verification failed
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Error message if verification failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The updated subscription state after verification
        /// </summary>
        public SubscriptionDto? Subscription { get; set; }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static VerifySubscriptionResultDto Successful(SubscriptionDto subscription)
        {
            return new VerifySubscriptionResultDto
            {
                Success = true,
                Subscription = subscription
            };
        }

        /// <summary>
        /// Creates a disabled result
        /// </summary>
        public static VerifySubscriptionResultDto Disabled()
        {
            return new VerifySubscriptionResultDto
            {
                Success = false,
                ErrorCode = "VERIFICATION_DISABLED",
                ErrorMessage = "Subscription verification is disabled on this environment."
            };
        }

        /// <summary>
        /// Creates a failed result
        /// </summary>
        public static VerifySubscriptionResultDto Failed(string errorCode, string errorMessage)
        {
            return new VerifySubscriptionResultDto
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }
}
