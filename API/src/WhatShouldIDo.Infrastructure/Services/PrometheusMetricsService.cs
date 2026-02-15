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
        private readonly Counter<long> _aiProviderSelectedTotal;
        private readonly Counter<long> _aiCallSuccessTotal;
        private readonly Counter<long> _aiCallFailuresTotal;
        private readonly Histogram<double> _aiCallLatencySeconds;
        private readonly Histogram<double> _routeGenerationDurationSeconds;

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

            // AI Provider Metrics
            _aiProviderSelectedTotal = _meter.CreateCounter<long>(
                "ai_provider_selected_total",
                description: "Total number of AI provider selections");

            _aiCallSuccessTotal = _meter.CreateCounter<long>(
                "ai_call_success_total",
                description: "Total number of successful AI API calls");

            _aiCallFailuresTotal = _meter.CreateCounter<long>(
                "ai_call_failures_total",
                description: "Total number of failed AI API calls");

            _aiCallLatencySeconds = _meter.CreateHistogram<double>(
                "ai_call_latency_seconds",
                unit: "s",
                description: "AI API call latency in seconds");

            _routeGenerationDurationSeconds = _meter.CreateHistogram<double>(
                "route_generation_duration_seconds",
                unit: "s",
                description: "AI route generation duration in seconds");
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

        public void RecordAIProviderSelected(string providerName)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("provider", providerName)
            };

            _aiProviderSelectedTotal.Add(1, tags);
        }

        public void RecordAICallLatency(string providerName, string operation, double durationSeconds)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("provider", providerName),
                new("operation", operation)
            };

            _aiCallLatencySeconds.Record(durationSeconds, tags);
        }

        public void IncrementAICallSuccess(string providerName)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("provider", providerName)
            };

            _aiCallSuccessTotal.Add(1, tags);
        }

        public void IncrementAICallFailure(string providerName, string reason)
        {
            var tags = new KeyValuePair<string, object?>[]
            {
                new("provider", providerName),
                new("reason", reason)
            };

            _aiCallFailuresTotal.Add(1, tags);
        }

        public void RecordRouteGenerationDuration(double durationSeconds)
        {
            _routeGenerationDurationSeconds.Record(durationSeconds);
        }

        public void RecordHistogram(string name, double value, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        {
            // Use the generic histogram for dynamic metrics
            var histogram = _meter.CreateHistogram<double>(name);
            if (tags != null)
            {
                histogram.Record(value, tags.ToArray());
            }
            else
            {
                histogram.Record(value);
            }
        }

        public void IncrementCounter(string name, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        {
            // Use dynamic counter for generic metrics
            var counter = _meter.CreateCounter<long>(name);
            if (tags != null)
            {
                counter.Add(1, tags.ToArray());
            }
            else
            {
                counter.Add(1);
            }
        }
    }
}