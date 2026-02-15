using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    /// <summary>
    /// Repository for UserTasteProfile with optimistic concurrency support.
    /// </summary>
    public class TasteProfileRepository : ITasteProfileRepository
    {
        private readonly WhatShouldIDoDbContext _context;

        public TasteProfileRepository(WhatShouldIDoDbContext context)
        {
            _context = context;
        }

        public async Task<UserTasteProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.UserTasteProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        }

        public async Task<UserTasteProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.UserTasteProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<UserTasteProfile> CreateAsync(UserTasteProfile profile, CancellationToken cancellationToken = default)
        {
            _context.UserTasteProfiles.Add(profile);
            await _context.SaveChangesAsync(cancellationToken);
            return profile;
        }

        public async Task<UserTasteProfile> UpdateAsync(UserTasteProfile profile, CancellationToken cancellationToken = default)
        {
            // Attach and mark as modified
            _context.UserTasteProfiles.Update(profile);

            // SaveChanges will throw DbUpdateConcurrencyException if RowVersion doesn't match
            await _context.SaveChangesAsync(cancellationToken);

            return profile;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var profile = await _context.UserTasteProfiles
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (profile != null)
            {
                _context.UserTasteProfiles.Remove(profile);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.UserTasteProfiles
                .AnyAsync(p => p.UserId == userId, cancellationToken);
        }
    }
}
