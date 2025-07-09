using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : EntityBase
    {
        protected readonly WhatShouldIDoDbContext _dbContext;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(WhatShouldIDoDbContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = _dbContext.Set<T>();
        }

        public async Task<T> GetByIdAsync(Guid id) =>
            await _dbSet.FindAsync(id) ?? throw new KeyNotFoundException($"{typeof(T).Name} with id {id} not found.");

        public async Task<IEnumerable<T>> GetAllAsync() =>
            await _dbSet.AsNoTracking().ToListAsync();

        public async Task AddAsync(T entity) =>
            await _dbSet.AddAsync(entity);

        public void Update(T entity) =>
            _dbSet.Update(entity);

        public void Remove(T entity) =>
            _dbSet.Remove(entity);

        public async Task SaveChangesAsync() =>
            await _dbContext.SaveChangesAsync();
    }
}