using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public class RoutePointRepository : GenericRepository<RoutePoint>, IRoutePointRepository
    {
        public RoutePointRepository(WhatShouldIDoDbContext dbContext) : base(dbContext) { }

        public async Task<IEnumerable<RoutePoint>> GetByRouteIdAsync(Guid routeId) =>
            await _dbContext.RoutePoints
                .Where(rp => rp.RouteId == routeId)
                .AsNoTracking()
                .ToListAsync();
    }
}
