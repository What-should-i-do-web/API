using MediatR;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.UseCases.Queries
{
    /// <summary>
    /// Query to retrieve all revisions for a route
    /// </summary>
    public class GetRouteRevisionsQuery : IRequest<List<RouteRevisionDto>>
    {
        /// <summary>
        /// The route ID
        /// </summary>
        public Guid RouteId { get; set; }

        /// <summary>
        /// User requesting revisions (for authorization check)
        /// </summary>
        public Guid UserId { get; set; }
    }
}
