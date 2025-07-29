using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IRoutePointService
    {
        Task<RoutePointDto> CreateAsync(CreateRoutePointRequest request);
        Task<IEnumerable<RoutePointDto>> GetByRouteAsync(Guid routeId);
        Task<RoutePointDto> GetByIdAsync(Guid id);
        Task<RoutePointDto> UpdateAsync(Guid id, UpdateRoutePointRequest request);
        Task DeleteAsync(Guid id);
    }
}
