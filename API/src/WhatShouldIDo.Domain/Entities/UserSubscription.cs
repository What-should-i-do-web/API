using System.ComponentModel.DataAnnotations;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Domain.Entities
{
    /// <summary>
    /// Represents a user's subscription state - provider-agnostic design
    /// supporting Apple App Store, Google Play, and manual grants.
    /// One active subscription per user (1:1 relationship).
    /// </summary>
    public class UserSubscription
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Foreign key to User entity
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// The provider where the subscription was purchased
        /// </summary>
        public SubscriptionProvider Provider { get; set; } = SubscriptionProvider.None;

        /// <summary>
        /// The subscription plan type
        /// </summary>
        public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;

        /// <summary>
        /// Current status of the subscription
        /// </summary>
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.None;

        /// <summary>
        /// When the trial period ends (only set when Status is Trialing)
        /// </summary>
        public DateTime? TrialEndsAtUtc { get; set; }

        /// <summary>
        /// When the current billing period ends
        /// </summary>
        public DateTime? CurrentPeriodEndsAtUtc { get; set; }

        /// <summary>
        /// Whether auto-renewal is enabled
        /// </summary>
        public bool AutoRenew { get; set; } = false;

        /// <summary>
        /// External subscription ID from Apple/Google
        /// </summary>
        public string? ExternalSubscriptionId { get; set; }

        /// <summary>
        /// Last time the subscription was verified with the provider
        /// </summary>
        public DateTime? LastVerifiedAtUtc { get; set; }

        /// <summary>
        /// Notes for manual grants only (records reason for grant).
        /// INVARIANT: Must be null when Provider != Manual.
        /// Should not contain PII or sensitive information.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Concurrency token for optimistic concurrency
        /// </summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        /// <summary>
        /// When this record was created
        /// </summary>
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this record was last updated
        /// </summary>
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual User? User { get; set; }

        #region Domain Behavior Methods

        /// <summary>
        /// Checks if the subscription is active at the given time
        /// </summary>
        public bool IsActiveAt(DateTime utcNow)
        {
            if (Status != SubscriptionStatus.Active)
                return false;

            // If no period end set, consider it active (lifetime or admin grant)
            if (CurrentPeriodEndsAtUtc == null)
                return true;

            return CurrentPeriodEndsAtUtc > utcNow;
        }

        /// <summary>
        /// Checks if the subscription is in trial period at the given time
        /// </summary>
        public bool IsTrialingAt(DateTime utcNow)
        {
            if (Status != SubscriptionStatus.Trialing)
                return false;

            if (TrialEndsAtUtc == null)
                return false;

            return TrialEndsAtUtc > utcNow;
        }

        /// <summary>
        /// Checks if user has premium entitlement (active or trialing)
        /// </summary>
        public bool HasEntitlementAt(DateTime utcNow)
        {
            return IsActiveAt(utcNow) || IsTrialingAt(utcNow);
        }

        /// <summary>
        /// Returns the effective plan at the given time
        /// </summary>
        public SubscriptionPlan EffectivePlanAt(DateTime utcNow)
        {
            return HasEntitlementAt(utcNow) ? Plan : SubscriptionPlan.Free;
        }

        /// <summary>
        /// Activates a subscription with the given parameters.
        /// Use this for IAP (Apple/Google) subscriptions.
        /// </summary>
        public void Activate(
            SubscriptionProvider provider,
            SubscriptionPlan plan,
            DateTime currentPeriodEndsAtUtc,
            DateTime utcNow,
            string? externalSubscriptionId = null,
            bool autoRenew = true)
        {
            if (provider == SubscriptionProvider.None)
                throw new InvalidOperationException("Cannot activate with Provider.None. Use ResetToFree() instead.");
            if (provider == SubscriptionProvider.Manual)
                throw new InvalidOperationException("Use GrantManual() for manual subscriptions.");
            if (plan == SubscriptionPlan.Free)
                throw new InvalidOperationException("Cannot activate Free plan.");

            Provider = provider;
            Plan = plan;
            Status = SubscriptionStatus.Active;
            CurrentPeriodEndsAtUtc = currentPeriodEndsAtUtc;
            ExternalSubscriptionId = externalSubscriptionId;
            AutoRenew = autoRenew;
            TrialEndsAtUtc = null; // Clear trial when activating
            Notes = null; // Clear notes for IAP
            LastVerifiedAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
        }

        /// <summary>
        /// Starts a trial period.
        /// Use this for IAP (Apple/Google) trials.
        /// </summary>
        public void StartTrial(
            SubscriptionProvider provider,
            SubscriptionPlan plan,
            DateTime trialEndsAtUtc,
            DateTime utcNow,
            string? externalSubscriptionId = null)
        {
            if (provider == SubscriptionProvider.None)
                throw new InvalidOperationException("Cannot start trial with Provider.None.");
            if (provider == SubscriptionProvider.Manual)
                throw new InvalidOperationException("Manual grants should use GrantManual(), not trials.");
            if (plan == SubscriptionPlan.Free)
                throw new InvalidOperationException("Cannot start trial for Free plan.");
            if (trialEndsAtUtc <= utcNow)
                throw new InvalidOperationException("TrialEndsAtUtc must be in the future.");

            Provider = provider;
            Plan = plan;
            Status = SubscriptionStatus.Trialing;
            TrialEndsAtUtc = trialEndsAtUtc;
            CurrentPeriodEndsAtUtc = trialEndsAtUtc; // Trial end is also period end
            ExternalSubscriptionId = externalSubscriptionId;
            AutoRenew = true; // Trials typically auto-renew to paid
            Notes = null;
            LastVerifiedAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
        }

        /// <summary>
        /// Marks the subscription as canceled (still has access until period end)
        /// </summary>
        public void Cancel(DateTime utcNow)
        {
            Status = SubscriptionStatus.Canceled;
            AutoRenew = false;
            UpdatedAtUtc = utcNow;
        }

        /// <summary>
        /// Marks the subscription as expired (no more access)
        /// </summary>
        public void Expire(DateTime utcNow)
        {
            Status = SubscriptionStatus.Expired;
            AutoRenew = false;
            UpdatedAtUtc = utcNow;
        }

        /// <summary>
        /// Updates the last verified timestamp
        /// </summary>
        public void MarkVerified(DateTime utcNow)
        {
            LastVerifiedAtUtc = utcNow;
            UpdatedAtUtc = utcNow;
        }

        /// <summary>
        /// Resets to free tier
        /// </summary>
        public void ResetToFree(DateTime utcNow)
        {
            Provider = SubscriptionProvider.None;
            Plan = SubscriptionPlan.Free;
            Status = SubscriptionStatus.None;
            TrialEndsAtUtc = null;
            CurrentPeriodEndsAtUtc = null;
            AutoRenew = false;
            ExternalSubscriptionId = null;
            LastVerifiedAtUtc = null;
            Notes = null;
            UpdatedAtUtc = utcNow;
        }

        /// <summary>
        /// Grants a manual subscription (internal/admin use only).
        /// INVARIANT: ExternalSubscriptionId will be set to null.
        /// </summary>
        /// <param name="plan">Monthly or Yearly (not Free)</param>
        /// <param name="currentPeriodEndsAtUtc">When the grant expires</param>
        /// <param name="utcNow">Current UTC time</param>
        /// <param name="notes">Reason for grant (no PII, max 500 chars)</param>
        public void GrantManual(
            SubscriptionPlan plan,
            DateTime currentPeriodEndsAtUtc,
            DateTime utcNow,
            string? notes = null)
        {
            if (plan == SubscriptionPlan.Free)
                throw new InvalidOperationException("Cannot manually grant Free plan. Use ResetToFree() instead.");

            if (currentPeriodEndsAtUtc <= utcNow)
                throw new InvalidOperationException("CurrentPeriodEndsAtUtc must be in the future.");

            Provider = SubscriptionProvider.Manual;
            Plan = plan;
            Status = SubscriptionStatus.Active;
            CurrentPeriodEndsAtUtc = currentPeriodEndsAtUtc;
            ExternalSubscriptionId = null; // Invariant: Manual grants have no external ID
            AutoRenew = false; // Manual grants don't auto-renew
            TrialEndsAtUtc = null;
            LastVerifiedAtUtc = utcNow;
            Notes = notes?.Length > 500 ? notes[..500] : notes;
            UpdatedAtUtc = utcNow;
        }

        #endregion

        #region Invariant Validation

        /// <summary>
        /// Validates all domain invariants. Returns list of violations.
        /// </summary>
        public IReadOnlyList<string> ValidateInvariants()
        {
            var violations = new List<string>();

            // Invariant 1: If Provider == None then Status must be None and Plan must be Free
            if (Provider == SubscriptionProvider.None)
            {
                if (Status != SubscriptionStatus.None)
                    violations.Add("When Provider is None, Status must be None.");
                if (Plan != SubscriptionPlan.Free)
                    violations.Add("When Provider is None, Plan must be Free.");
            }

            // Invariant 2: If Provider == Manual then ExternalSubscriptionId must be null
            if (Provider == SubscriptionProvider.Manual && ExternalSubscriptionId != null)
            {
                violations.Add("When Provider is Manual, ExternalSubscriptionId must be null.");
            }

            // Invariant 3: If Status == Trialing then TrialEndsAtUtc must be set
            if (Status == SubscriptionStatus.Trialing && TrialEndsAtUtc == null)
            {
                violations.Add("When Status is Trialing, TrialEndsAtUtc must be set.");
            }

            // Invariant 4: If Status == Active then CurrentPeriodEndsAtUtc should be set
            // (Exception: lifetime/unlimited grants may have null - but we recommend always setting it)
            if (Status == SubscriptionStatus.Active && Provider != SubscriptionProvider.None && CurrentPeriodEndsAtUtc == null)
            {
                // This is a warning, not strictly an error for manual lifetime grants
                // But for IAP it should always be set
                if (Provider == SubscriptionProvider.AppleAppStore || Provider == SubscriptionProvider.GooglePlay)
                {
                    violations.Add("When Status is Active with IAP provider, CurrentPeriodEndsAtUtc must be set.");
                }
            }

            // Invariant 5: Notes should only be set for Manual provider
            if (Notes != null && Provider != SubscriptionProvider.Manual)
            {
                violations.Add("Notes field should only be set when Provider is Manual.");
            }

            return violations;
        }

        /// <summary>
        /// Throws if any invariants are violated.
        /// </summary>
        public void EnsureInvariantsOrThrow()
        {
            var violations = ValidateInvariants();
            if (violations.Count > 0)
            {
                throw new InvalidOperationException(
                    $"UserSubscription invariant violations: {string.Join("; ", violations)}");
            }
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates a default free tier subscription for a new user.
        /// Satisfies invariant: Provider=None implies Status=None and Plan=Free.
        /// </summary>
        public static UserSubscription CreateDefault(Guid userId, DateTime utcNow)
        {
            return new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Provider = SubscriptionProvider.None,
                Plan = SubscriptionPlan.Free,
                Status = SubscriptionStatus.None,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            };
        }

        #endregion
    }
}
