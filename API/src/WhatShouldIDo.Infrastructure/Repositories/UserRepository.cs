using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly WhatShouldIDoDbContext _context;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(WhatShouldIDoDbContext context, ILogger<UserRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == id && u.IsActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by ID: {UserId}", id);
                throw;
            }
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email: {Email}", email);
                throw;
            }
        }

        public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserName.ToLower() == username.ToLower() && u.IsActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by username: {Username}", username);
                throw;
            }
        }

        public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email existence: {Email}", email);
                throw;
            }
        }

        public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.UserName.ToLower() == username.ToLower() && u.IsActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking username existence: {Username}", username);
                throw;
            }
        }

        public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
        {
            try
            {
                user.CreatedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                user.LastApiReset = DateTime.UtcNow;

                _context.Users.Add(user);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("User created successfully: {UserId} - {Email}", user.Id, user.Email);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Email}", user.Email);
                throw;
            }
        }

        public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            try
            {
                user.UpdatedAt = DateTime.UtcNow;
                
                _context.Users.Update(user);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("User updated successfully: {UserId}", user.Id);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
                if (user == null)
                    return false;

                // Soft delete
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("User soft deleted: {UserId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId}", id);
                throw;
            }
        }

        public async Task<int> IncrementApiUsageAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if daily usage needs reset (new day)
                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user == null)
                    throw new InvalidOperationException($"User {userId} not found");

                var today = DateTime.UtcNow.Date;
                if (user.LastApiReset.Date < today)
                {
                    user.DailyApiUsage = 0;
                    user.LastApiReset = DateTime.UtcNow;
                }

                user.DailyApiUsage++;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);
                
                return user.DailyApiUsage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing API usage for user: {UserId}", userId);
                throw;
            }
        }

        public async Task<int> ResetDailyUsageForAllUsersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var usersToReset = await _context.Users
                    .Where(u => u.IsActive && u.LastApiReset.Date < today)
                    .ToListAsync(cancellationToken);

                foreach (var user in usersToReset)
                {
                    user.DailyApiUsage = 0;
                    user.LastApiReset = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Daily usage reset for {Count} users", usersToReset.Count);
                return usersToReset.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting daily usage for all users");
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetUsersWithExpiredSubscriptionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                return await _context.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && u.SubscriptionExpiry.HasValue && u.SubscriptionExpiry < now && u.SubscriptionTier != SubscriptionTier.Free)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users with expired subscriptions");
                throw;
            }
        }

        public async Task<User?> GetWithProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == id && u.IsActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with profile: {UserId}", id);
                throw;
            }
        }
    }
}