using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public interface IRoutePointRepository : IGenericRepository<RoutePoint>
    {
        Task<IEnumerable<RoutePoint>> GetByRouteIdAsync(Guid routeId);
    }
}
