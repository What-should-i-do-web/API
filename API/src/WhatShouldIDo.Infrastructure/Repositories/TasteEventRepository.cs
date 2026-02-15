using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    /// <summary>
    /// Repository for UserTasteEvent (append-only audit trail).
    /// </summary>
    public class TasteEventRepository : ITasteEventRepository
    {
        private readonly WhatShouldIDoDbContext _context;

        public TasteEventRepository(WhatShouldIDoDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserTasteEvent tasteEvent, CancellationToken cancellationToken = default)
        {
            _context.UserTasteEvents.Add(tasteEvent);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<UserTasteEvent>> GetByUserIdAsync(
            Guid userId,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserTasteEvents
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.OccurredAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<UserTasteEvent>> GetByUserIdAndTypeAsync(
            Guid userId,
            string eventType,
            int skip = 0,
            int take = 50,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserTasteEvents
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.EventType == eventType)
                .OrderByDescending(e => e.OccurredAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<UserTasteEvent>> GetByUserIdAndTimeRangeAsync(
            Guid userId,
            DateTime startUtc,
            DateTime endUtc,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserTasteEvents
                .AsNoTracking()
                .Where(e => e.UserId == userId
                    && e.OccurredAtUtc >= startUtc
                    && e.OccurredAtUtc <= endUtc)
                .OrderByDescending(e => e.OccurredAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.UserTasteEvents
                .CountAsync(e => e.UserId == userId, cancellationToken);
        }
    }
}
