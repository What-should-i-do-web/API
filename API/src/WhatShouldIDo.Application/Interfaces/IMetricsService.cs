using System;

namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Service for recording application metrics (Prometheus-compatible).
    /// All methods are fire-and-forget for minimal performance impact.
    /// Consolidates both quota-specific and general application metrics.
    /// </summary>
    public interface IMetricsService
    {
        // ===== HTTP Request Metrics =====

        /// <summary>
        /// Records an API request completion with comprehensive dimensions.
        /// Increments requests_total counter and updates request_duration_seconds histogram.
        /// </summary>
        /// <param name="endpoint">The endpoint name (e.g., "PromptController.Generate")</param>
        /// <param name="method">HTTP method (GET, POST, etc.)</param>
        /// <param name="statusCode">HTTP status code (200, 401, 403, 500, etc.)</param>
        /// <param name="durationMs">Request duration in milliseconds</param>
        /// <param name="isAuthenticated">Whether the request was authenticated</param>
        /// <param name="isPremium">Whether the user is premium (null if not authenticated)</param>
        void RecordRequest(
            string endpoint,
            string method,
            int statusCode,
            double durationMs,
            bool isAuthenticated,
            bool? isPremium);

        /// <summary>
        /// Records an API request (legacy method for backward compatibility).
        /// </summary>
        /// <param name="endpoint">The endpoint name</param>
        /// <param name="method">HTTP method</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="duration">Request duration in seconds</param>
        void RecordApiRequest(string endpoint, string method, int statusCode, double duration);

        // ===== Quota Metrics =====

        /// <summary>
        /// Records current quota remaining for a user.
        /// Updates quota_remaining gauge.
        /// </summary>
        /// <param name="userIdHash">Hashed user ID for safe cardinality</param>
        /// <param name="remaining">Remaining quota value</param>
        void RecordQuotaRemaining(string userIdHash, int remaining);

        /// <summary>
        /// Records quota consumption.
        /// Increments quota_consumed_total counter.
        /// </summary>
        /// <param name="amount">Amount of quota consumed (usually 1)</param>
        void RecordQuotaConsumed(int amount = 1);

        /// <summary>
        /// Records a quota block (request denied due to exhausted quota).
        /// Increments quota_blocked_total counter and quota_users_with_zero gauge.
        /// </summary>
        void RecordQuotaBlocked();

        /// <summary>
        /// Records an entitlement check.
        /// Increments entitlement_checks_total counter.
        /// </summary>
        /// <param name="source">Source of the check (claim, redis, db)</param>
        /// <param name="outcome">Outcome (premium, free, error)</param>
        void RecordEntitlementCheck(string source, string outcome);

        /// <summary>
        /// Records Redis quota script execution.
        /// Updates redis_quota_script_latency_seconds histogram.
        /// </summary>
        /// <param name="operation">Redis operation name (e.g., "consume", "get")</param>
        /// <param name="durationMs">Operation duration in milliseconds</param>
        /// <param name="success">Whether the operation succeeded</param>
        void RecordRedisOperation(string operation, double durationMs, bool success);

        /// <summary>
        /// Records a Redis error.
        /// Increments redis_errors_total counter.
        /// </summary>
        /// <param name="operation">Redis operation that failed</param>
        void RecordRedisError(string operation);

        /// <summary>
        /// Records a database subscription read.
        /// Increments db_subscription_reads_total counter and updates db_latency_seconds histogram.
        /// </summary>
        /// <param name="outcome">Outcome (success, not_found, error)</param>
        /// <param name="durationMs">Operation duration in milliseconds</param>
        void RecordDatabaseRead(string outcome, double durationMs);

        /// <summary>
        /// Records a webhook event.
        /// Increments webhook_events_total counter.
        /// </summary>
        /// <param name="eventType">Type of webhook event (e.g., "subscription.created")</param>
        /// <param name="outcome">Outcome (success, failure)</param>
        void RecordWebhookEvent(string eventType, string outcome);

        /// <summary>
        /// Records a webhook signature verification failure.
        /// Increments webhook_verify_failures_total counter.
        /// </summary>
        void RecordWebhookVerificationFailure();

        /// <summary>
        /// Records a rate limit block.
        /// Increments rate_limit_blocks_total counter.
        /// </summary>
        /// <param name="endpoint">The endpoint that was rate limited</param>
        void RecordRateLimitBlock(string endpoint);

        // ===== Cache Metrics (Legacy) =====

        /// <summary>
        /// Records a cache hit.
        /// </summary>
        /// <param name="cacheType">The type of cache (redis, memory, etc.)</param>
        void RecordCacheHit(string cacheType);

        /// <summary>
        /// Records a cache miss.
        /// </summary>
        /// <param name="cacheType">The type of cache (redis, memory, etc.)</param>
        void RecordCacheMiss(string cacheType);

        // ===== Database Metrics (Legacy) =====

        /// <summary>
        /// Records a database query execution.
        /// </summary>
        /// <param name="duration">Query duration in seconds</param>
        /// <param name="isSlowQuery">Whether the query exceeded slow query threshold</param>
        void RecordDatabaseQuery(double duration, bool isSlowQuery);

        // ===== Rate Limit Metrics (Legacy) =====

        /// <summary>
        /// Records a rate limit hit.
        /// </summary>
        /// <param name="tier">The rate limit tier</param>
        /// <param name="clientType">The client type</param>
        void RecordRateLimitHit(string tier, string clientType);

        // ===== Business Metrics (Legacy) =====

        /// <summary>
        /// Records a place search operation.
        /// </summary>
        /// <param name="provider">The search provider</param>
        /// <param name="resultCount">Number of results returned</param>
        /// <param name="duration">Search duration in seconds</param>
        void RecordPlaceSearch(string provider, int resultCount, double duration);

        /// <summary>
        /// Increments the active users counter.
        /// </summary>
        void IncrementActiveUsers();

        /// <summary>
        /// Decrements the active users counter.
        /// </summary>
        void DecrementActiveUsers();
    }
}
