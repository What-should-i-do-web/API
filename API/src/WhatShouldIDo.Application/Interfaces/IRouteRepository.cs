using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Route entities
    /// </summary>
    public interface IRouteRepository
    {
        /// <summary>
        /// Get route by ID
        /// </summary>
        Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all routes
        /// </summary>
        Task<IEnumerable<Route>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get routes by user ID
        /// </summary>
        Task<IEnumerable<Route>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get routes by name (search)
        /// </summary>
        Task<IEnumerable<Route>> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Add new route
        /// </summary>
        Task<Route> AddAsync(Route route, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update existing route
        /// </summary>
        Task<Route> UpdateAsync(Route route, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete route
        /// </summary>
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Save changes to database
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // ===== Share Token Operations =====

        /// <summary>
        /// Creates a share token for a route
        /// </summary>
        Task<RouteShareToken> CreateShareTokenAsync(Guid routeId, Guid userId, DateTime? expiresAt = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a share token by its token string
        /// </summary>
        Task<RouteShareToken?> GetShareTokenAsync(string token, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all share tokens for a route
        /// </summary>
        Task<IEnumerable<RouteShareToken>> GetShareTokensByRouteIdAsync(Guid routeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deactivates a share token
        /// </summary>
        Task<bool> DeactivateShareTokenAsync(string token, Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records an access to a shared route
        /// </summary>
        Task RecordShareAccessAsync(string token, CancellationToken cancellationToken = default);

        // ===== Revision Operations =====

        /// <summary>
        /// Creates a new revision for a route
        /// </summary>
        Task<RouteRevision> CreateRevisionAsync(Guid routeId, string routeDataJson, Guid userId, string source, string? changeDescription = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all revisions for a route
        /// </summary>
        Task<IEnumerable<RouteRevision>> GetRevisionsAsync(Guid routeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific revision by ID
        /// </summary>
        Task<RouteRevision?> GetRevisionByIdAsync(Guid revisionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the latest revision number for a route
        /// </summary>
        Task<int> GetLatestRevisionNumberAsync(Guid routeId, CancellationToken cancellationToken = default);
    }
}
