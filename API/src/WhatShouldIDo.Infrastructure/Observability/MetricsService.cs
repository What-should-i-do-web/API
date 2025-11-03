using System;
using System.Diagnostics.Metrics;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Observability
{
    /// <summary>
    /// Implementation of IMetricsService using OpenTelemetry Metrics API.
    /// All metrics are Prometheus-compatible via OTLP exporter.
    /// </summary>
    public class MetricsService : IMetricsService
    {
        private readonly Meter _meter;

        // Counters
        private readonly Counter<long> _requestsTotal;
        private readonly Counter<long> _quotaConsumedTotal;
        private readonly Counter<long> _quotaBlockedTotal;
        private readonly Counter<long> _entitlementChecksTotal;
        private readonly Counter<long> _redisErrorsTotal;
        private readonly Counter<long> _dbSubscriptionReadsTotal;
        private readonly Counter<long> _webhookEventsTotal;
        private readonly Counter<long> _webhookVerifyFailuresTotal;
        private readonly Counter<long> _rateLimitBlocksTotal;
        private readonly Counter<long> _cacheHitsTotal;
        private readonly Counter<long> _cacheMissesTotal;
        private readonly Counter<long> _slowQueriesTotal;
        private readonly Counter<long> _rateLimitHitsTotal;
        private readonly Counter<long> _placeSearchesTotal;

        // Histograms
        private readonly Histogram<double> _requestDurationSeconds;
        private readonly Histogram<double> _redisLatencySeconds;
        private readonly Histogram<double> _dbLatencySeconds;
        private readonly Histogram<double> _databaseQueryDuration;
        private readonly Histogram<double> _placeSearchDuration;

        // Gauges (implemented via ObservableGauge in constructor if needed)
        private readonly ObservableGauge<int> _quotaUsersWithZero;
        private readonly UpDownCounter<long> _activeUsers;

        private int _usersWithZeroQuota = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsService"/> class.
        /// </summary>
        /// <param name="meterFactory">The meter factory (injected from DI)</param>
        public MetricsService(IMeterFactory meterFactory)
        {
            _meter = meterFactory.Create("WhatShouldIDo.API", "1.0.0");

            // Initialize counters
            _requestsTotal = _meter.CreateCounter<long>(
                "requests_total",
                description: "Total number of HTTP requests");

            _quotaConsumedTotal = _meter.CreateCounter<long>(
                "quota_consumed_total",
                description: "Total quota credits consumed");

            _quotaBlockedTotal = _meter.CreateCounter<long>(
                "quota_blocked_total",
                description: "Total requests blocked due to quota exhaustion");

            _entitlementChecksTotal = _meter.CreateCounter<long>(
                "entitlement_checks_total",
                description: "Total entitlement checks performed");

            _redisErrorsTotal = _meter.CreateCounter<long>(
                "redis_errors_total",
                description: "Total Redis operation errors");

            _dbSubscriptionReadsTotal = _meter.CreateCounter<long>(
                "db_subscription_reads_total",
                description: "Total database subscription reads");

            _webhookEventsTotal = _meter.CreateCounter<long>(
                "webhook_events_total",
                description: "Total webhook events processed");

            _webhookVerifyFailuresTotal = _meter.CreateCounter<long>(
                "webhook_verify_failures_total",
                description: "Total webhook signature verification failures");

            _rateLimitBlocksTotal = _meter.CreateCounter<long>(
                "rate_limit_blocks_total",
                description: "Total requests blocked by rate limiting");

            // Initialize histograms with SLO-aligned buckets
            _requestDurationSeconds = _meter.CreateHistogram<double>(
                "request_duration_seconds",
                unit: "s",
                description: "Request duration in seconds",
                advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = new[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
                });

            _redisLatencySeconds = _meter.CreateHistogram<double>(
                "redis_quota_script_latency_seconds",
                unit: "s",
                description: "Redis quota script execution latency",
                advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = new[] { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5 }
                });

            _dbLatencySeconds = _meter.CreateHistogram<double>(
                "db_latency_seconds",
                unit: "s",
                description: "Database operation latency",
                advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.2, 0.5, 1.0 }
                });

            // Initialize observable gauges
            _quotaUsersWithZero = _meter.CreateObservableGauge(
                "quota_users_with_zero",
                () => _usersWithZeroQuota,
                description: "Number of users currently with zero quota");

            // Initialize legacy metrics
            _cacheHitsTotal = _meter.CreateCounter<long>(
                "cache_hits_total",
                description: "Total number of cache hits");

            _cacheMissesTotal = _meter.CreateCounter<long>(
                "cache_misses_total",
                description: "Total number of cache misses");

            _slowQueriesTotal = _meter.CreateCounter<long>(
                "slow_queries_total",
                description: "Total number of slow database queries");

            _rateLimitHitsTotal = _meter.CreateCounter<long>(
                "rate_limit_hits_total",
                description: "Total number of rate limit hits");

            _placeSearchesTotal = _meter.CreateCounter<long>(
                "place_searches_total",
                description: "Total number of place searches");

            _databaseQueryDuration = _meter.CreateHistogram<double>(
                "database_query_duration_seconds",
                unit: "s",
                description: "Database query duration in seconds",
                advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.2, 0.5, 1.0 }
                });

            _placeSearchDuration = _meter.CreateHistogram<double>(
                "place_search_duration_seconds",
                unit: "s",
                description: "Place search duration in seconds",
                advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = new[] { 0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0 }
                });

            _activeUsers = _meter.CreateUpDownCounter<long>(
                "active_users",
                description: "Number of active users");
        }

        /// <inheritdoc/>
        public void RecordRequest(
            string endpoint,
            string method,
            int statusCode,
            double durationMs,
            bool isAuthenticated,
            bool? isPremium)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("endpoint", endpoint),
                new KeyValuePair<string, object?>("method", method),
                new KeyValuePair<string, object?>("status_code", statusCode),
                new KeyValuePair<string, object?>("authenticated", isAuthenticated),
                new KeyValuePair<string, object?>("premium", isPremium?.ToString() ?? "unknown")
            };

            _requestsTotal.Add(1, tags);
            _requestDurationSeconds.Record(durationMs / 1000.0, tags);
        }

        /// <inheritdoc/>
        public void RecordQuotaRemaining(string userIdHash, int remaining)
        {
            // Note: Per-user gauges would create high cardinality.
            // For production, we track aggregates and use logs/traces for per-user debugging.
            // This implementation focuses on aggregate metrics.
            if (remaining == 0)
            {
                System.Threading.Interlocked.Increment(ref _usersWithZeroQuota);
            }
        }

        /// <inheritdoc/>
        public void RecordQuotaConsumed(int amount = 1)
        {
            _quotaConsumedTotal.Add(amount);
        }

        /// <inheritdoc/>
        public void RecordQuotaBlocked()
        {
            _quotaBlockedTotal.Add(1);
        }

        /// <inheritdoc/>
        public void RecordEntitlementCheck(string source, string outcome)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("source", source),
                new KeyValuePair<string, object?>("outcome", outcome)
            };

            _entitlementChecksTotal.Add(1, tags);
        }

        /// <inheritdoc/>
        public void RecordRedisOperation(string operation, double durationMs, bool success)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("success", success)
            };

            _redisLatencySeconds.Record(durationMs / 1000.0, tags);

            if (!success)
            {
                RecordRedisError(operation);
            }
        }

        /// <inheritdoc/>
        public void RecordRedisError(string operation)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("operation", operation)
            };

            _redisErrorsTotal.Add(1, tags);
        }

        /// <inheritdoc/>
        public void RecordDatabaseRead(string outcome, double durationMs)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("outcome", outcome)
            };

            _dbSubscriptionReadsTotal.Add(1, tags);
            _dbLatencySeconds.Record(durationMs / 1000.0, tags);
        }

        /// <inheritdoc/>
        public void RecordWebhookEvent(string eventType, string outcome)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("type", eventType),
                new KeyValuePair<string, object?>("outcome", outcome)
            };

            _webhookEventsTotal.Add(1, tags);
        }

        /// <inheritdoc/>
        public void RecordWebhookVerificationFailure()
        {
            _webhookVerifyFailuresTotal.Add(1);
        }

        /// <inheritdoc/>
        public void RecordRateLimitBlock(string endpoint)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("endpoint", endpoint)
            };

            _rateLimitBlocksTotal.Add(1, tags);
        }

        // ===== Legacy Methods (Backward Compatibility) =====

        /// <inheritdoc/>
        public void RecordApiRequest(string endpoint, string method, int statusCode, double duration)
        {
            // Convert duration from seconds to milliseconds and call the new method
            RecordRequest(endpoint, method, statusCode, duration * 1000.0, isAuthenticated: true, isPremium: null);
        }

        /// <inheritdoc/>
        public void RecordCacheHit(string cacheType)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("cache_type", cacheType)
            };

            _cacheHitsTotal.Add(1, tags);
        }

        /// <inheritdoc/>
        public void RecordCacheMiss(string cacheType)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("cache_type", cacheType)
            };

            _cacheMissesTotal.Add(1, tags);
        }

        /// <inheritdoc/>
        public void RecordDatabaseQuery(double duration, bool isSlowQuery)
        {
            _databaseQueryDuration.Record(duration);

            if (isSlowQuery)
            {
                _slowQueriesTotal.Add(1);
            }
        }

        /// <inheritdoc/>
        public void RecordRateLimitHit(string tier, string clientType)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("tier", tier),
                new KeyValuePair<string, object?>("client_type", clientType)
            };

            _rateLimitHitsTotal.Add(1, tags);
        }

        /// <inheritdoc/>
        public void RecordPlaceSearch(string provider, int resultCount, double duration)
        {
            var tags = new[]
            {
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("result_count_bucket", GetResultCountBucket(resultCount))
            };

            _placeSearchesTotal.Add(1, tags);
            _placeSearchDuration.Record(duration, tags);
        }

        /// <inheritdoc/>
        public void IncrementActiveUsers()
        {
            _activeUsers.Add(1);
        }

        /// <inheritdoc/>
        public void DecrementActiveUsers()
        {
            _activeUsers.Add(-1);
        }

        /// <summary>
        /// Gets the result count bucket for grouping search results.
        /// </summary>
        /// <param name="count">The result count</param>
        /// <returns>Bucket label</returns>
        private static string GetResultCountBucket(int count)
        {
            return count switch
            {
                0 => "0",
                <= 5 => "1-5",
                <= 10 => "6-10",
                <= 20 => "11-20",
                <= 50 => "21-50",
                _ => "50+"
            };
        }
    }
}
