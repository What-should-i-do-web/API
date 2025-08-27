using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
        Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<int> IncrementApiUsageAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> ResetDailyUsageForAllUsersAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetUsersWithExpiredSubscriptionsAsync(CancellationToken cancellationToken = default);
        Task<User?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default);
    }
}