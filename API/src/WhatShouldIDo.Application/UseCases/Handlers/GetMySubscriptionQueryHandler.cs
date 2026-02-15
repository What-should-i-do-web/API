using MediatR;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Application.UseCases.Queries;

namespace WhatShouldIDo.Application.UseCases.Handlers
{
    /// <summary>
    /// Handler for getting the current user's subscription
    /// </summary>
    public class GetMySubscriptionQueryHandler : IRequestHandler<GetMySubscriptionQuery, SubscriptionDto>
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<GetMySubscriptionQueryHandler> _logger;

        public GetMySubscriptionQueryHandler(
            ISubscriptionService subscriptionService,
            ILogger<GetMySubscriptionQueryHandler> logger)
        {
            _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SubscriptionDto> Handle(GetMySubscriptionQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting subscription for user: {UserId}", request.UserId);

            var subscription = await _subscriptionService.GetMySubscriptionAsync(request.UserId, cancellationToken);

            _logger.LogInformation(
                "Retrieved subscription for user {UserId}: Plan={Plan}, Status={Status}, HasEntitlement={HasEntitlement}",
                request.UserId, subscription.Plan, subscription.Status, subscription.HasEntitlement);

            return subscription;
        }
    }
}
