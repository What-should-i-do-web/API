using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public interface IGenericRepository<T> where T : EntityBase
    {
        Task<T> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();
        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);
        Task SaveChangesAsync();
    }
}