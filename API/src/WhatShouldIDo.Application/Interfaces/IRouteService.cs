using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IRouteService
    {
        Task<RouteDto> CreateAsync(CreateRouteRequest request);
        Task<IEnumerable<RouteDto>> GetAllAsync();
        Task<RouteDto> UpdateAsync(Guid id, UpdateRouteRequest request);
        Task DeleteAsync(Guid id);
    }
}
