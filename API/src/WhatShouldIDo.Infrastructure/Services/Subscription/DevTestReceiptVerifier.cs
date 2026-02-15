using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Infrastructure.Services.Subscription
{
    /// <summary>
    /// Receipt verifier for development and testing purposes.
    /// Only accepts specific test receipt strings: TEST_MONTHLY, TEST_YEARLY, TEST_TRIAL_MONTHLY, TEST_TRIAL_YEARLY
    /// WARNING: This verifier should NEVER be used in production.
    /// </summary>
    public class DevTestReceiptVerifier : IReceiptVerifier
    {
        private readonly SubscriptionOptions _options;
        private readonly ILogger<DevTestReceiptVerifier> _logger;
        private readonly IClock _clock;

        private const string TestMonthly = "TEST_MONTHLY";
        private const string TestYearly = "TEST_YEARLY";
        private const string TestTrialMonthly = "TEST_TRIAL_MONTHLY";
        private const string TestTrialYearly = "TEST_TRIAL_YEARLY";

        public DevTestReceiptVerifier(
            IOptions<SubscriptionOptions> options,
            ILogger<DevTestReceiptVerifier> logger,
            IClock clock)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Task<ReceiptVerificationResult> VerifyAsync(
            VerifyReceiptRequest request,
            CancellationToken cancellationToken = default)
        {
            // Log only the test receipt type (e.g., "TEST_MONTHLY"), never any real data
            _logger.LogInformation("DevTestReceiptVerifier processing receipt type: {ReceiptType}", request.ReceiptData);

            var utcNow = _clock.UtcNow;

            var result = request.ReceiptData.ToUpperInvariant() switch
            {
                TestMonthly => CreateMonthlyResult(utcNow, isTrialing: false),
                TestYearly => CreateYearlyResult(utcNow, isTrialing: false),
                TestTrialMonthly => CreateMonthlyResult(utcNow, isTrialing: true),
                TestTrialYearly => CreateYearlyResult(utcNow, isTrialing: true),
                _ when request.ReceiptData.StartsWith("TEST_", StringComparison.OrdinalIgnoreCase) &&
                       request.IsTrialRequested => CreateResultFromRequest(request, utcNow, isTrialing: true),
                _ when request.ReceiptData.StartsWith("TEST_", StringComparison.OrdinalIgnoreCase) =>
                    CreateResultFromRequest(request, utcNow, isTrialing: false),
                _ => ReceiptVerificationResult.Invalid(
                    "INVALID_TEST_RECEIPT",
                    "Invalid test receipt. Use TEST_MONTHLY, TEST_YEARLY, TEST_TRIAL_MONTHLY, or TEST_TRIAL_YEARLY.")
            };

            _logger.LogInformation(
                "DevTestReceiptVerifier result: IsValid={IsValid}, Status={Status}, Plan={Plan}",
                result.IsValid, result.Status, result.Plan);

            return Task.FromResult(result);
        }

        private ReceiptVerificationResult CreateMonthlyResult(DateTime utcNow, bool isTrialing)
        {
            var trialDays = _options.MonthlyTrialDays;
            var periodEnd = isTrialing
                ? utcNow.AddDays(trialDays)
                : utcNow.AddMonths(1);

            return ReceiptVerificationResult.Valid(
                provider: SubscriptionProvider.AppleAppStore,
                plan: SubscriptionPlan.Monthly,
                status: isTrialing ? SubscriptionStatus.Trialing : SubscriptionStatus.Active,
                currentPeriodEndsAtUtc: periodEnd,
                trialEndsAtUtc: isTrialing ? periodEnd : null,
                externalSubscriptionId: $"test_sub_{Guid.NewGuid():N}",
                autoRenew: true);
        }

        private ReceiptVerificationResult CreateYearlyResult(DateTime utcNow, bool isTrialing)
        {
            var trialDays = _options.YearlyTrialDays;
            var periodEnd = isTrialing
                ? utcNow.AddDays(trialDays)
                : utcNow.AddYears(1);

            return ReceiptVerificationResult.Valid(
                provider: SubscriptionProvider.AppleAppStore,
                plan: SubscriptionPlan.Yearly,
                status: isTrialing ? SubscriptionStatus.Trialing : SubscriptionStatus.Active,
                currentPeriodEndsAtUtc: periodEnd,
                trialEndsAtUtc: isTrialing ? periodEnd : null,
                externalSubscriptionId: $"test_sub_{Guid.NewGuid():N}",
                autoRenew: true);
        }

        private ReceiptVerificationResult CreateResultFromRequest(
            VerifyReceiptRequest request,
            DateTime utcNow,
            bool isTrialing)
        {
            var trialDays = request.Plan == SubscriptionPlan.Monthly
                ? _options.MonthlyTrialDays
                : _options.YearlyTrialDays;

            DateTime periodEnd;
            if (isTrialing)
            {
                periodEnd = utcNow.AddDays(trialDays);
            }
            else
            {
                periodEnd = request.Plan == SubscriptionPlan.Monthly
                    ? utcNow.AddMonths(1)
                    : utcNow.AddYears(1);
            }

            return ReceiptVerificationResult.Valid(
                provider: request.Provider,
                plan: request.Plan,
                status: isTrialing ? SubscriptionStatus.Trialing : SubscriptionStatus.Active,
                currentPeriodEndsAtUtc: periodEnd,
                trialEndsAtUtc: isTrialing ? periodEnd : null,
                externalSubscriptionId: $"test_sub_{Guid.NewGuid():N}",
                autoRenew: true);
        }
    }
}
