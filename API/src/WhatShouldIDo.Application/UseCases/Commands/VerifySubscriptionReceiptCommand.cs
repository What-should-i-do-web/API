using MediatR;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Domain.Enums;

namespace WhatShouldIDo.Application.UseCases.Commands
{
    /// <summary>
    /// Command to verify a subscription receipt and update user subscription
    /// </summary>
    public class VerifySubscriptionReceiptCommand : IRequest<VerifySubscriptionResultDto>
    {
        /// <summary>
        /// The user's ID
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// The provider of the receipt (Apple or Google)
        /// </summary>
        public SubscriptionProvider Provider { get; set; }

        /// <summary>
        /// The subscription plan being verified
        /// </summary>
        public SubscriptionPlan Plan { get; set; }

        /// <summary>
        /// The receipt data from the store.
        /// SECURITY: Never log raw receipt data.
        /// </summary>
        public string ReceiptData { get; set; } = string.Empty;

        /// <summary>
        /// Backward-compatible alias for ReceiptData.
        /// </summary>
        [Obsolete("Use ReceiptData instead. This property is kept for backward compatibility.")]
        public string Receipt
        {
            get => ReceiptData;
            set => ReceiptData = value;
        }

        /// <summary>
        /// Whether the user is requesting a trial
        /// </summary>
        public bool IsTrialRequested { get; set; }

        public VerifySubscriptionReceiptCommand(Guid userId, VerifyReceiptRequest request)
        {
            UserId = userId;
            Provider = request.Provider;
            Plan = request.Plan;
            ReceiptData = request.ReceiptData;
            IsTrialRequested = request.IsTrialRequested;
        }

        /// <summary>
        /// Converts to VerifyReceiptRequest for service calls
        /// </summary>
        public VerifyReceiptRequest ToRequest()
        {
            return new VerifyReceiptRequest
            {
                Provider = Provider,
                Plan = Plan,
                ReceiptData = ReceiptData,
                IsTrialRequested = IsTrialRequested
            };
        }
    }
}
