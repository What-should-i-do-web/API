namespace WhatShouldIDo.Domain.Enums
{
    /// <summary>
    /// Subscription provider - where the subscription was purchased.
    /// IMPORTANT: None means "no subscription / free tier" and must never grant entitlement.
    /// </summary>
    public enum SubscriptionProvider
    {
        /// <summary>
        /// No provider - user is on free tier with no subscription record or has no active subscription.
        /// INVARIANT: If Provider == None then Status must be None and Plan must be Free.
        /// </summary>
        None = 0,

        /// <summary>
        /// Internal/admin manual grant (not via IAP).
        /// Used for promotional access, support cases, or internal testing.
        /// INVARIANT: ExternalSubscriptionId must be null when Provider == Manual.
        /// </summary>
        Manual = 1,

        /// <summary>
        /// Apple App Store In-App Purchase
        /// </summary>
        AppleAppStore = 2,

        /// <summary>
        /// Google Play Store In-App Purchase
        /// </summary>
        GooglePlay = 3
    }

    /// <summary>
    /// Subscription plan type
    /// </summary>
    public enum SubscriptionPlan
    {
        /// <summary>
        /// Free tier - default for all users
        /// </summary>
        Free = 0,

        /// <summary>
        /// Monthly subscription
        /// </summary>
        Monthly = 1,

        /// <summary>
        /// Yearly subscription
        /// </summary>
        Yearly = 2
    }

    /// <summary>
    /// Subscription status representing the current state
    /// </summary>
    public enum SubscriptionStatus
    {
        /// <summary>
        /// No subscription - free tier user
        /// </summary>
        None = 0,

        /// <summary>
        /// User is in trial period
        /// </summary>
        Trialing = 1,

        /// <summary>
        /// Active paid subscription
        /// </summary>
        Active = 2,

        /// <summary>
        /// Payment failed but grace period active
        /// </summary>
        PastDue = 3,

        /// <summary>
        /// User canceled but still has access until period end
        /// </summary>
        Canceled = 4,

        /// <summary>
        /// Subscription has expired - no access
        /// </summary>
        Expired = 5
    }
}
