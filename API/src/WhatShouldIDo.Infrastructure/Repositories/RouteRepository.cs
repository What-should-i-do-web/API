using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public class RouteRepository : GenericRepository<Route>, IRouteRepository
    {
        public RouteRepository(WhatShouldIDoDbContext dbContext) : base(dbContext) { }

        public async Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Routes
                .Include(r => r.Points)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Route>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Routes
                .Include(r => r.Points)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Route>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Routes
                .Include(r => r.Points)
                .Where(r => r.UserId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Route>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Routes
                .Include(r => r.Points)
                .Where(r => r.Name.Contains(name))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<Route> AddAsync(Route route, CancellationToken cancellationToken = default)
        {
            await _dbContext.Routes.AddAsync(route, cancellationToken);
            return route;
        }

        public Task<Route> UpdateAsync(Route route, CancellationToken cancellationToken = default)
        {
            _dbContext.Routes.Update(route);
            return Task.FromResult(route);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var route = await _dbContext.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (route == null) return false;

            _dbContext.Routes.Remove(route);
            return true;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // ===== Share Token Operations =====

        public async Task<RouteShareToken> CreateShareTokenAsync(
            Guid routeId,
            Guid userId,
            DateTime? expiresAt = null,
            CancellationToken cancellationToken = default)
        {
            var shareToken = new RouteShareToken(routeId, userId, expiresAt);
            await _dbContext.RouteShareTokens.AddAsync(shareToken, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return shareToken;
        }

        public async Task<RouteShareToken?> GetShareTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            return await _dbContext.RouteShareTokens
                .Include(st => st.Route)
                .ThenInclude(r => r!.Points)
                .FirstOrDefaultAsync(st => st.Token == token, cancellationToken);
        }

        public async Task<IEnumerable<RouteShareToken>> GetShareTokensByRouteIdAsync(
            Guid routeId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.RouteShareTokens
                .Where(st => st.RouteId == routeId)
                .OrderByDescending(st => st.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> DeactivateShareTokenAsync(
            string token,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var shareToken = await _dbContext.RouteShareTokens
                .Include(st => st.Route)
                .FirstOrDefaultAsync(st => st.Token == token, cancellationToken);

            if (shareToken == null)
                return false;

            // Verify ownership
            if (shareToken.CreatedByUserId != userId)
                return false;

            shareToken.Deactivate();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task RecordShareAccessAsync(string token, CancellationToken cancellationToken = default)
        {
            var shareToken = await _dbContext.RouteShareTokens
                .FirstOrDefaultAsync(st => st.Token == token, cancellationToken);

            if (shareToken != null)
            {
                shareToken.RecordAccess();
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        // ===== Revision Operations =====

        public async Task<RouteRevision> CreateRevisionAsync(
            Guid routeId,
            string routeDataJson,
            Guid userId,
            string source,
            string? changeDescription = null,
            CancellationToken cancellationToken = default)
        {
            // Get next revision number
            var nextRevisionNumber = await GetLatestRevisionNumberAsync(routeId, cancellationToken) + 1;

            var revision = new RouteRevision(
                routeId,
                nextRevisionNumber,
                routeDataJson,
                userId,
                source,
                changeDescription);

            await _dbContext.RouteRevisions.AddAsync(revision, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return revision;
        }

        public async Task<IEnumerable<RouteRevision>> GetRevisionsAsync(
            Guid routeId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.RouteRevisions
                .Where(r => r.RouteId == routeId)
                .OrderByDescending(r => r.RevisionNumber)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<RouteRevision?> GetRevisionByIdAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.RouteRevisions
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == revisionId, cancellationToken);
        }

        public async Task<int> GetLatestRevisionNumberAsync(
            Guid routeId,
            CancellationToken cancellationToken = default)
        {
            var latestRevision = await _dbContext.RouteRevisions
                .Where(r => r.RouteId == routeId)
                .OrderByDescending(r => r.RevisionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            return latestRevision?.RevisionNumber ?? 0;
        }
    }
}