using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public class RouteRepository : GenericRepository<Route>, IRouteRepository
    {
        public RouteRepository(WhatShouldIDoDbContext dbContext) : base(dbContext) { }

        public async Task<IEnumerable<Route>> GetByNameAsync(string name) =>
            await _dbContext.Routes
                .Where(r => r.Name.Contains(name))
                .AsNoTracking()
                .ToListAsync();
    }
}