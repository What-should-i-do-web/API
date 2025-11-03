using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace; // <-- AddException() buradan gelir
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Quota
{
    /// <summary>
    /// Decorator for RedisQuotaStore that adds OpenTelemetry instrumentation
    /// (traces and metrics) to all Redis operations.
    /// </summary>
    public class InstrumentedRedisQuotaStore : IQuotaStore
    {
        private readonly IQuotaStore _inner;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<InstrumentedRedisQuotaStore> _logger;
        private readonly ActivitySource _activitySource;

        public InstrumentedRedisQuotaStore(
            IQuotaStore inner,
            IMetricsService metricsService,
            ILogger<InstrumentedRedisQuotaStore> logger)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _activitySource = new ActivitySource("WhatShouldIDo.Redis", "1.0.0");
        }

        public async Task<int?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            using var activity = _activitySource.StartActivity("redis.quota.get", ActivityKind.Client);
            activity?.SetTag("redis.operation", "GET");
            activity?.SetTag("user.id", userId.ToString());

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await _inner.GetAsync(userId, cancellationToken);
                stopwatch.Stop();

                _metricsService.RecordRedisOperation("get", stopwatch.Elapsed.TotalMilliseconds, success: true);

                activity?.SetTag("redis.quota_value", result?.ToString() ?? "null");
                activity?.SetStatus(ActivityStatusCode.Ok);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metricsService.RecordRedisOperation("get", stopwatch.Elapsed.TotalMilliseconds, success: false);
                _metricsService.RecordRedisError("get");

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                _logger.LogError(ex, "Redis GET operation failed for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> CompareExchangeConsumeAsync(Guid userId, int amount, CancellationToken cancellationToken = default)
        {
            using var activity = _activitySource.StartActivity("redis.quota.consume", ActivityKind.Client);
            activity?.SetTag("redis.operation", "CONSUME");
            activity?.SetTag("user.id", userId.ToString());
            activity?.SetTag("quota.amount", amount);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await _inner.CompareExchangeConsumeAsync(userId, amount, cancellationToken);
                stopwatch.Stop();

                _metricsService.RecordRedisOperation("consume", stopwatch.Elapsed.TotalMilliseconds, success: true);

                activity?.SetTag("redis.consume_success", result);
                activity?.SetStatus(ActivityStatusCode.Ok);

                if (result)
                    _metricsService.RecordQuotaConsumed(amount);
                else
                    _metricsService.RecordQuotaBlocked();

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metricsService.RecordRedisOperation("consume", stopwatch.Elapsed.TotalMilliseconds, success: false);
                _metricsService.RecordRedisError("consume");

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                _logger.LogError(ex, "Redis CONSUME operation failed for user {UserId}", userId);
                throw;
            }
        }

        public async Task SetAsync(Guid userId, int value, CancellationToken cancellationToken = default)
        {
            using var activity = _activitySource.StartActivity("redis.quota.set", ActivityKind.Client);
            activity?.SetTag("redis.operation", "SET");
            activity?.SetTag("user.id", userId.ToString());
            activity?.SetTag("quota.value", value);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _inner.SetAsync(userId, value, cancellationToken);
                stopwatch.Stop();

                _metricsService.RecordRedisOperation("set", stopwatch.Elapsed.TotalMilliseconds, success: true);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metricsService.RecordRedisOperation("set", stopwatch.Elapsed.TotalMilliseconds, success: false);
                _metricsService.RecordRedisError("set");

                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.AddException(ex);

                _logger.LogError(ex, "Redis SET operation failed for user {UserId}", userId);
                throw;
            }
        }
    }
}
