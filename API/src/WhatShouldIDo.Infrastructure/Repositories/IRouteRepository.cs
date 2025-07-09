using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public interface IRouteRepository : IGenericRepository<Route>
    {
        Task<IEnumerable<Route>> GetByNameAsync(string name);
    }
}