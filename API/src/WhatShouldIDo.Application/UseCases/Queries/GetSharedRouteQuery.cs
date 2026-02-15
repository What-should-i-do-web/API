using MediatR;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.UseCases.Queries
{
    /// <summary>
    /// Query to retrieve a shared route using a share token
    /// </summary>
    public class GetSharedRouteQuery : IRequest<SharedRouteDto?>
    {
        /// <summary>
        /// The share token
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }
}
