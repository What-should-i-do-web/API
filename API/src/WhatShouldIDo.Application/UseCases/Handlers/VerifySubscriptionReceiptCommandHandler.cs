using MediatR;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Commands;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for verifying subscription receipts
    /// </summary>
    public class VerifySubscriptionReceiptCommandHandler : IRequestHandler<VerifySubscriptionReceiptCommand, VerifySubscriptionResultDto>
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<VerifySubscriptionReceiptCommandHandler> _logger;

        public VerifySubscriptionReceiptCommandHandler(
            ISubscriptionService subscriptionService,
            ILogger<VerifySubscriptionReceiptCommandHandler> logger)
        {
            _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<VerifySubscriptionResultDto> Handle(
            VerifySubscriptionReceiptCommand request,
            CancellationToken cancellationToken)
        {
            // Log without exposing the receipt content (security)
            _logger.LogInformation(
                "Verifying subscription receipt for user: {UserId}, Provider: {Provider}, Plan: {Plan}",
                request.UserId, request.Provider, request.Plan);

            var result = await _subscriptionService.VerifyReceiptAsync(
                request.UserId,
                request.ToRequest(),
                cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Subscription verification successful for user {UserId}: Plan={Plan}, Status={Status}",
                    request.UserId, result.Subscription?.Plan, result.Subscription?.Status);
            }
            else
            {
                _logger.LogWarning(
                    "Subscription verification failed for user {UserId}: ErrorCode={ErrorCode}, Message={ErrorMessage}",
                    request.UserId, result.ErrorCode, result.ErrorMessage);
            }

            return result;
        }
    }
}
