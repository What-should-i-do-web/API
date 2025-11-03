using System.Diagnostics.Metrics;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{

    public class PrometheusMetricsService : IMetricsService, IDisposable
    {
        private readonly Meter _meter;
        private readonly Counter<long> _httpRequestsTotal;
        private readonly Histogram<double> _httpRequestDuration;
        private readonly Counter<long> _cacheHitsTotal;
        private readonly Counter<long> _cacheMissesTotal;
        private readonly Histogram<double> _databaseQueryDuration;
        private readonly Counter<long> _slowQueriesTotal;
        private readonly Counter<long> _rateLimitHitsTotal;
        private readonly Counter<long> _placeSearchesTotal;
        private readonly Histogram<double> _placeSearchDuration;
        private readonly UpDownCounter<long> _activeUsers;

        public PrometheusMetricsService()
        {
            _meter = new Meter("WhatShouldIDo.API", "1.0.0");

            // HTTP Request Metrics
            _httpRequestsTotal = _meter.CreateCounter<long>(
                "http_requests_total",
                description: "Total number of HTTP requests");

            _httpRequestDuration = _meter.CreateHistogram<double>(
                "http_request_duration_seconds",
                unit: "s",
                description: "HTTP request duration in seconds");

            // Cache Metrics
            _cacheHitsTotal = _meter.CreateCounter<long>(
                "cache_hits_total",
                description: "Total number of cache hits");

            _cacheMissesTotal = _meter.CreateCounter<long>(
                "cache_misses_total", 
                description: "Total number of cache misses");

            // Database Metrics
            _databaseQueryDuration = _meter.CreateHistogram<double>(
                "database_query_duration_seconds",
                unit: "s",
                description: "Database query duration in seconds");

            _slowQueriesTotal = _meter.CreateCounter<long>(
                "slow_queries_total",
                description: "Total number of slow database queries");

            // Rate Limiting Metrics
            _rateLimitHitsTotal = _meter.CreateCounter<long>(
                "rate_limit_hits_total",
                description: "Total number of rate limit hits");

            // Business Metrics
            _placeSearchesTotal = _meter.CreateCounter<long>(
                "place_searches_total",
                description: "Total number of place searches");

            _placeSearchDuration = _meter.CreateHistogram<double>(
                "place_search_duration_seconds",
                unit: "s", 
                description: "Place search duration in seconds");

            _activeUsers = _meter.CreateUpDownCounter<long>(
                "active_users",
                description: "Number of active users");
        }

        public void RecordApiRequest(string endpoint, string method, int statusCode, double duration)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("endpoint", endpoint),
                new("method", method),
                new("status_code", statusCode.ToString())
            };

            _httpRequestsTotal.Add(1, tags);
            _httpRequestDuration.Record(duration, tags);
        }

        public void RecordCacheHit(string cacheType)
        {
            _cacheHitsTotal.Add(1, new KeyValuePair<string, object?>("cache_type", cacheType));
        }

        public void RecordCacheMiss(string cacheType)
        {
            _cacheMissesTotal.Add(1, new KeyValuePair<string, object?>("cache_type", cacheType));
        }

        public void RecordDatabaseQuery(double duration, bool isSlowQuery)
        {
            _databaseQueryDuration.Record(duration);
            
            if (isSlowQuery)
            {
                _slowQueriesTotal.Add(1);
            }
        }

        public void RecordRateLimitHit(string tier, string clientType)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("tier", tier),
                new("client_type", clientType)
            };
            
            _rateLimitHitsTotal.Add(1, tags);
        }

        public void RecordPlaceSearch(string provider, int resultCount, double duration)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("provider", provider),
                new("result_count_bucket", GetResultCountBucket(resultCount))
            };

            _placeSearchesTotal.Add(1, tags);
            _placeSearchDuration.Record(duration, tags);
        }

        public void IncrementActiveUsers()
        {
            _activeUsers.Add(1);
        }

        public void DecrementActiveUsers()
        {
            _activeUsers.Add(-1);
        }

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

        public void Dispose()
        {
            _meter?.Dispose();
        }

        public void RecordRequest(string endpoint, string method, int statusCode, double durationMs, bool isAuthenticated, bool? isPremium)
        {
            throw new NotImplementedException();
        }

        public void RecordQuotaRemaining(string userIdHash, int remaining)
        {
            throw new NotImplementedException();
        }

        public void RecordQuotaConsumed(int amount = 1)
        {
            throw new NotImplementedException();
        }

        public void RecordQuotaBlocked()
        {
            throw new NotImplementedException();
        }

        public void RecordEntitlementCheck(string source, string outcome)
        {
            throw new NotImplementedException();
        }

        public void RecordRedisOperation(string operation, double durationMs, bool success)
        {
            throw new NotImplementedException();
        }

        public void RecordRedisError(string operation)
        {
            throw new NotImplementedException();
        }

        public void RecordDatabaseRead(string outcome, double durationMs)
        {
            throw new NotImplementedException();
        }

        public void RecordWebhookEvent(string eventType, string outcome)
        {
            throw new NotImplementedException();
        }

        public void RecordWebhookVerificationFailure()
        {
            throw new NotImplementedException();
        }

        public void RecordRateLimitBlock(string endpoint)
        {
            throw new NotImplementedException();
        }
    }
}