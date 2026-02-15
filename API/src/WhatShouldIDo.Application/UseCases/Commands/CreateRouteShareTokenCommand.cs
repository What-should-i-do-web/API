using MediatR;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.UseCases.Commands
{
    /// <summary>
    /// Command to create a share token for a route
    /// </summary>
    public class CreateRouteShareTokenCommand : IRequest<RouteShareTokenDto>
    {
        /// <summary>
        /// The route to share
        /// </summary>
        public Guid RouteId { get; set; }

        /// <summary>
        /// User creating the share token (must be route owner)
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Optional expiration date for the share link
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
