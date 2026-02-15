using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing user history, favorites, and exclusions.
    /// Implements MRU (Most Recently Used) pattern with auto-pruning.
    /// </summary>
    public class UserHistoryRepository : IUserHistoryRepository
    {
        private readonly WhatShouldIDoDbContext _context;
        private readonly ILogger<UserHistoryRepository> _logger;

        // MRU limits as per requirements
        private const int MAX_ROUTE_HISTORY = 3;
        private const int MAX_SUGGESTION_HISTORY = 20;

        public UserHistoryRepository(WhatShouldIDoDbContext context, ILogger<UserHistoryRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============== Favorites Management ==============

        public async Task<UserFavorite> AddFavoriteAsync(
            Guid userId,
            string placeId,
            string placeName,
            string? category = null,
            double? latitude = null,
            double? longitude = null,
            string? notes = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if already favorited
                var existing = await _context.Set<UserFavorite>()
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.PlaceId == placeId, cancellationToken);

                if (existing != null)
                {
                    _logger.LogInformation("Place {PlaceId} already favorited by user {UserId}", placeId, userId);
                    return existing;
                }

                var favorite = new UserFavorite
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PlaceId = placeId,
                    PlaceName = placeName,
                    Category = category,
                    Latitude = latitude,
                    Longitude = longitude,
                    Notes = notes,
                    AddedAt = DateTime.UtcNow
                };

                await _context.Set<UserFavorite>().AddAsync(favorite, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added favorite place {PlaceId} for user {UserId}", placeId, userId);
                return favorite;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding favorite place {PlaceId} for user {UserId}", placeId, userId);
                throw;
            }
        }

        public async Task<bool> RemoveFavoriteAsync(
            Guid userId,
            string placeId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var favorite = await _context.Set<UserFavorite>()
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.PlaceId == placeId, cancellationToken);

                if (favorite == null)
                {
                    _logger.LogWarning("Favorite place {PlaceId} not found for user {UserId}", placeId, userId);
                    return false;
                }

                _context.Set<UserFavorite>().Remove(favorite);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Removed favorite place {PlaceId} for user {UserId}", placeId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing favorite place {PlaceId} for user {UserId}", placeId, userId);
                throw;
            }
        }

        public async Task<IEnumerable<UserFavorite>> GetUserFavoritesAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Set<UserFavorite>()
                    .AsNoTracking()
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.AddedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving favorites for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> IsFavoriteAsync(
            Guid userId,
            string placeId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Set<UserFavorite>()
                    .AsNoTracking()
                    .AnyAsync(f => f.UserId == userId && f.PlaceId == placeId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if place {PlaceId} is favorite for user {UserId}", placeId, userId);
                throw;
            }
        }

        // ============== Exclusions Management ==============

        public async Task<UserExclusion> AddExclusionAsync(
            Guid userId,
            string placeId,
            string placeName,
            DateTime? expiresAt = null,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if already excluded
                var existing = await _context.Set<UserExclusion>()
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.PlaceId == placeId, cancellationToken);

                if (existing != null)
                {
                    // Update expiration if needed
                    existing.ExpiresAt = expiresAt;
                    existing.Reason = reason ?? existing.Reason;
                    existing.ExcludedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Updated exclusion for place {PlaceId} for user {UserId}", placeId, userId);
                    return existing;
                }

                var exclusion = new UserExclusion
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PlaceId = placeId,
                    PlaceName = placeName,
                    ExcludedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    Reason = reason
                };

                await _context.Set<UserExclusion>().AddAsync(exclusion, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added exclusion for place {PlaceId} for user {UserId}", placeId, userId);
                return exclusion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding exclusion for place {PlaceId} for user {UserId}", placeId, userId);
                throw;
            }
        }

        public async Task<bool> RemoveExclusionAsync(
            Guid userId,
            string placeId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var exclusion = await _context.Set<UserExclusion>()
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.PlaceId == placeId, cancellationToken);

                if (exclusion == null)
                {
                    _logger.LogWarning("Exclusion for place {PlaceId} not found for user {UserId}", placeId, userId);
                    return false;
                }

                _context.Set<UserExclusion>().Remove(exclusion);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Removed exclusion for place {PlaceId} for user {UserId}", placeId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing exclusion for place {PlaceId} for user {UserId}", placeId, userId);
                throw;
            }
        }

        public async Task<IEnumerable<UserExclusion>> GetActiveExclusionsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                return await _context.Set<UserExclusion>()
                    .AsNoTracking()
                    .Where(e => e.UserId == userId && (e.ExpiresAt == null || e.ExpiresAt > now))
                    .OrderByDescending(e => e.ExcludedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active exclusions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> IsExcludedAsync(
            Guid userId,
            string placeId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                return await _context.Set<UserExclusion>()
                    .AsNoTracking()
                    .AnyAsync(e => e.UserId == userId &&
                                  e.PlaceId == placeId &&
                                  (e.ExpiresAt == null || e.ExpiresAt > now),
                             cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if place {PlaceId} is excluded for user {UserId}", placeId, userId);
                throw;
            }
        }

        public async Task<int> CleanupExpiredExclusionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredExclusions = await _context.Set<UserExclusion>()
                    .Where(e => e.ExpiresAt != null && e.ExpiresAt <= now)
                    .ToListAsync(cancellationToken);

                if (expiredExclusions.Any())
                {
                    _context.Set<UserExclusion>().RemoveRange(expiredExclusions);
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Cleaned up {Count} expired exclusions", expiredExclusions.Count);
                }

                return expiredExclusions.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired exclusions");
                throw;
            }
        }

        // ============== Suggestion History (MRU - Last 20 Places) ==============

        public async Task AddSuggestionHistoryAsync(
            Guid userId,
            string placeId,
            string placeName,
            string? category = null,
            string source = "surprise_me",
            string? sessionId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var sequenceNumber = await GetNextSequenceNumberAsync(userId, cancellationToken);

                var history = new UserSuggestionHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PlaceId = placeId,
                    PlaceName = placeName,
                    Category = category,
                    SuggestedAt = DateTime.UtcNow,
                    Source = source,
                    SequenceNumber = sequenceNumber,
                    SessionId = sessionId
                };

                await _context.Set<UserSuggestionHistory>().AddAsync(history, cancellationToken);

                // Auto-prune: Keep only last MAX_SUGGESTION_HISTORY items
                await PruneSuggestionHistoryAsync(userId, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added suggestion history for place {PlaceId} for user {UserId}, sequence {SequenceNumber}",
                    placeId, userId, sequenceNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding suggestion history for place {PlaceId} for user {UserId}", placeId, userId);
                throw;
            }
        }

        public async Task AddSuggestionHistoryBatchAsync(
            Guid userId,
            IEnumerable<(string placeId, string placeName, string? category)> places,
            string source = "surprise_me",
            string? sessionId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var placeList = places.ToList();
                if (!placeList.Any())
                    return;

                // Generate session ID if not provided
                sessionId ??= Guid.NewGuid().ToString();

                var sequenceNumber = await GetNextSequenceNumberAsync(userId, cancellationToken);

                var histories = placeList.Select((place, index) => new UserSuggestionHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PlaceId = place.placeId,
                    PlaceName = place.placeName,
                    Category = place.category,
                    SuggestedAt = DateTime.UtcNow,
                    Source = source,
                    SequenceNumber = sequenceNumber + index,
                    SessionId = sessionId
                }).ToList();

                await _context.Set<UserSuggestionHistory>().AddRangeAsync(histories, cancellationToken);

                // Auto-prune: Keep only last MAX_SUGGESTION_HISTORY items
                await PruneSuggestionHistoryAsync(userId, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added {Count} suggestion histories in batch for user {UserId}, session {SessionId}",
                    placeList.Count, userId, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding suggestion history batch for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<UserSuggestionHistory>> GetRecentSuggestionsAsync(
            Guid userId,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Set<UserSuggestionHistory>()
                    .AsNoTracking()
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.SequenceNumber)
                    .Take(take)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent suggestions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetRecentlyExcludedPlaceIdsAsync(
            Guid userId,
            int exclusionWindowSize = 3,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Set<UserSuggestionHistory>()
                    .AsNoTracking()
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.SequenceNumber)
                    .Take(exclusionWindowSize)
                    .Select(s => s.PlaceId)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recently excluded place IDs for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Prunes suggestion history to keep only the last MAX_SUGGESTION_HISTORY items
        /// </summary>
        private async Task PruneSuggestionHistoryAsync(Guid userId, CancellationToken cancellationToken)
        {
            var toDelete = await _context.Set<UserSuggestionHistory>()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SequenceNumber)
                .Skip(MAX_SUGGESTION_HISTORY)
                .ToListAsync(cancellationToken);

            if (toDelete.Any())
            {
                _context.Set<UserSuggestionHistory>().RemoveRange(toDelete);
                _logger.LogInformation("Pruned {Count} old suggestion history items for user {UserId}", toDelete.Count, userId);
            }
        }

        // ============== Route History (MRU - Last 3 Routes) ==============

        public async Task AddRouteHistoryAsync(
            Guid userId,
            string routeName,
            string routeDataJson,
            int placeCount,
            Guid? routeId = null,
            string source = "surprise_me",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var sequenceNumber = await GetNextSequenceNumberAsync(userId, cancellationToken);

                var history = new UserRouteHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RouteId = routeId,
                    RouteName = routeName,
                    RouteDataJson = routeDataJson,
                    CreatedAt = DateTime.UtcNow,
                    SequenceNumber = sequenceNumber,
                    Source = source,
                    PlaceCount = placeCount
                };

                await _context.Set<UserRouteHistory>().AddAsync(history, cancellationToken);

                // Auto-prune: Keep only last MAX_ROUTE_HISTORY items
                await PruneRouteHistoryAsync(userId, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Added route history '{RouteName}' for user {UserId}, sequence {SequenceNumber}",
                    routeName, userId, sequenceNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding route history for user {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<UserRouteHistory>> GetUserRouteHistoryAsync(
            Guid userId,
            int take = 3,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Set<UserRouteHistory>()
                    .AsNoTracking()
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.SequenceNumber)
                    .Take(take)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving route history for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Prunes route history to keep only the last MAX_ROUTE_HISTORY items
        /// </summary>
        private async Task PruneRouteHistoryAsync(Guid userId, CancellationToken cancellationToken)
        {
            var toDelete = await _context.Set<UserRouteHistory>()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.SequenceNumber)
                .Skip(MAX_ROUTE_HISTORY)
                .ToListAsync(cancellationToken);

            if (toDelete.Any())
            {
                _context.Set<UserRouteHistory>().RemoveRange(toDelete);
                _logger.LogInformation("Pruned {Count} old route history items for user {UserId}", toDelete.Count, userId);
            }
        }

        // ============== Helper Methods ==============

        public async Task<long> GetNextSequenceNumberAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get max sequence number from both history tables
                var maxSuggestionSeq = await _context.Set<UserSuggestionHistory>()
                    .Where(s => s.UserId == userId)
                    .MaxAsync(s => (long?)s.SequenceNumber, cancellationToken) ?? 0;

                var maxRouteSeq = await _context.Set<UserRouteHistory>()
                    .Where(r => r.UserId == userId)
                    .MaxAsync(r => (long?)r.SequenceNumber, cancellationToken) ?? 0;

                return Math.Max(maxSuggestionSeq, maxRouteSeq) + 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next sequence number for user {UserId}", userId);
                throw;
            }
        }
    }
}
