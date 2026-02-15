using MediatR;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.UseCases.Queries
{
    /// <summary>
    /// Query to get the current user's subscription
    /// </summary>
    public class GetMySubscriptionQuery : IRequest<SubscriptionDto>
    {
        /// <summary>
        /// The user's ID
        /// </summary>
        public Guid UserId { get; set; }

        public GetMySubscriptionQuery(Guid userId)
        {
            UserId = userId;
        }
    }
}
