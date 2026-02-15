namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Cache service interface for storing and retrieving cached data
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Get cached value by key, or execute acquire function and cache result
        /// </summary>
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> acquire, TimeSpan? absoluteExpiration = null);

        /// <summary>
        /// Get cached value by key
        /// </summary>
        Task<T?> GetAsync<T>(string key) where T : class;

        /// <summary>
        /// Set cached value with expiration
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;

        /// <summary>
        /// Remove cached value by key
        /// </summary>
        Task RemoveAsync(string key);

        /// <summary>
        /// Check if key exists in cache
        /// </summary>
        Task<bool> ExistsAsync(string key);
    }
}
