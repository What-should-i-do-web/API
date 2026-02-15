using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Repository for managing user history, favorites, and exclusions.
    /// Implements MRU (Most Recently Used) pattern with auto-pruning.
    /// </summary>
    public interface IUserHistoryRepository
    {
        // ============== Favorites Management ==============

        /// <summary>
        /// Adds a place to user's favorites
        /// </summary>
        Task<UserFavorite> AddFavoriteAsync(
            Guid userId,
            string placeId,
            string placeName,
            string? category = null,
            double? latitude = null,
            double? longitude = null,
            string? notes = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a place from user's favorites
        /// </summary>
        Task<bool> RemoveFavoriteAsync(
            Guid userId,
            string placeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all user's favorite places
        /// </summary>
        Task<IEnumerable<UserFavorite>> GetUserFavoritesAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a place is in user's favorites
        /// </summary>
        Task<bool> IsFavoriteAsync(
            Guid userId,
            string placeId,
            CancellationToken cancellationToken = default);

        // ============== Exclusions Management ==============

        /// <summary>
        /// Adds a place to user's exclusion list (do not recommend)
        /// </summary>
        Task<UserExclusion> AddExclusionAsync(
            Guid userId,
            string placeId,
            string placeName,
            DateTime? expiresAt = null,
            string? reason = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a place from user's exclusion list
        /// </summary>
        Task<bool> RemoveExclusionAsync(
            Guid userId,
            string placeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all active (non-expired) exclusions for a user
        /// </summary>
        Task<IEnumerable<UserExclusion>> GetActiveExclusionsAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a place is currently excluded for the user (respects TTL)
        /// </summary>
        Task<bool> IsExcludedAsync(
            Guid userId,
            string placeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes expired exclusions from the database (cleanup task)
        /// </summary>
        Task<int> CleanupExpiredExclusionsAsync(CancellationToken cancellationToken = default);

        // ============== Suggestion History (MRU - Last 20 Places) ==============

        /// <summary>
        /// Adds a place to suggestion history with auto-pruning (keeps last 20).
        /// Uses sequence numbers for MRU ordering.
        /// </summary>
        Task AddSuggestionHistoryAsync(
            Guid userId,
            string placeId,
            string placeName,
            string? category = null,
            string source = "surprise_me",
            string? sessionId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds multiple places to suggestion history in a batch (for single request).
        /// Uses same session ID for grouping.
        /// </summary>
        Task AddSuggestionHistoryBatchAsync(
            Guid userId,
            IEnumerable<(string placeId, string placeName, string? category)> places,
            string source = "surprise_me",
            string? sessionId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets recent suggestions for a user (ordered by sequence number descending)
        /// </summary>
        Task<IEnumerable<UserSuggestionHistory>> GetRecentSuggestionsAsync(
            Guid userId,
            int take = 20,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets place IDs from the last N suggestions (for exclusion window logic).
        /// Default: last 3 suggestions as per exclusion window.
        /// </summary>
        Task<IEnumerable<string>> GetRecentlyExcludedPlaceIdsAsync(
            Guid userId,
            int exclusionWindowSize = 3,
            CancellationToken cancellationToken = default);

        // ============== Route History (MRU - Last 3 Routes) ==============

        /// <summary>
        /// Adds a route to route history with auto-pruning (keeps last 3).
        /// Uses sequence numbers for MRU ordering.
        /// </summary>
        Task AddRouteHistoryAsync(
            Guid userId,
            string routeName,
            string routeDataJson,
            int placeCount,
            Guid? routeId = null,
            string source = "surprise_me",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets user's route history (ordered by sequence number descending)
        /// </summary>
        Task<IEnumerable<UserRouteHistory>> GetUserRouteHistoryAsync(
            Guid userId,
            int take = 3,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the next sequence number for a user (for MRU ordering)
        /// </summary>
        Task<long> GetNextSequenceNumberAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
