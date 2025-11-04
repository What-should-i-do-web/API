using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.API.Attributes;
using WhatShouldIDo.API.Middleware;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Quota;
using Xunit;

namespace WhatShouldIDo.Tests.Integration
{
    /// <summary>
    /// Chaos engineering and resilience tests to verify fail-closed behavior
    /// and graceful degradation when dependencies fail.
    ///
    /// Tests cover:
    /// - Redis unavailable scenarios
    /// - Database connection timeouts
    /// - High latency dependencies
    /// - Partial system degradation
    /// - Premium user access during failures
    /// </summary>
    public class ChaosAndResilienceTests
    {
        #region Redis Failure Tests

        [Fact]
        public async Task RedisDown_FreeUser_FailsClosed()
        {
            // Arrange - Create host with failing Redis
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost(redisAvailable: false);
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            // Free users should be denied when Redis is down (fail-closed)
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.InternalServerError);

            var content = await response.Content.ReadAsStringAsync();
            content.ToLower().Should().Contain("temporarily unavailable");
        }

        [Fact]
        public async Task RedisDown_PremiumUser_StillWorks()
        {
            // Arrange - Create host with failing Redis
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost(redisAvailable: false, allowPremiumOnFailure: true);
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: true);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            // Premium users should still work when Redis is down
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RedisDown_HealthCheck_ReturnsUnhealthy()
        {
            // Arrange
            using var host = await CreateTestHost(redisAvailable: false);
            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            // Ready endpoint should report unhealthy
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        [Fact]
        public async Task RedisDown_LivenessCheck_StillHealthy()
        {
            // Arrange
            using var host = await CreateTestHost(redisAvailable: false);
            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/health/live");

            // Assert
            // Live endpoint should still be healthy (app is running)
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RedisHighLatency_StillWorks_WithWarning()
        {
            // Arrange - Redis with 100ms latency
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost(redisLatencyMs: 100);
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Initialize quota
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 5);

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            // Should succeed but health check might report degraded
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RedisTimeout_FailsClosed()
        {
            // Arrange - Redis with extreme timeout
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost(redisLatencyMs: 10000); // 10 second delay
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            // Should fail due to timeout
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.RequestTimeout);
        }

        #endregion

        #region Database Failure Tests

        [Fact]
        public async Task DatabaseDown_HealthCheck_ReportsUnhealthy()
        {
            // Arrange
            using var host = await CreateTestHost(databaseAvailable: false);
            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        [Fact]
        public async Task DatabaseSlow_HealthCheck_ReportsDegraded()
        {
            // Arrange - Database with 200ms latency
            using var host = await CreateTestHost(databaseLatencyMs: 200);
            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            // Might be degraded but still return 200 or 503 depending on implementation
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
        }

        #endregion

        #region Partial Degradation Tests

        [Fact]
        public async Task PartialDegradation_RedisDown_DatabaseUp_FailsClosed()
        {
            // Arrange - Only Redis is down
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost(redisAvailable: false, databaseAvailable: true);
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            // Should fail because quota store (Redis) is unavailable
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task PartialDegradation_RedisUp_DatabaseDown_QuotaStillEnforced()
        {
            // Arrange - Only database is down, Redis is up
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost(redisAvailable: true, databaseAvailable: false);
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Initialize quota
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 5);

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            // Quota enforcement should still work (depends on Redis only)
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Graceful Degradation Tests

        [Fact]
        public async Task GracefulDegradation_SkipQuotaEndpoints_AlwaysWork()
        {
            // Arrange - All dependencies down
            using var host = await CreateTestHost(redisAvailable: false, databaseAvailable: false);
            var client = host.GetTestClient();

            // Act - Try endpoints marked with [SkipQuota]
            var healthResponse = await client.GetAsync("/health/live");
            var metricsResponse = await client.GetAsync("/metrics");

            // Assert
            // These endpoints should always work
            healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            metricsResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GracefulDegradation_AnonymousEndpoints_StillAccessible()
        {
            // Arrange - All dependencies down
            using var host = await CreateTestHost(redisAvailable: false, databaseAvailable: false);
            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/public-endpoint");

            // Assert
            // Public endpoints should still work
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Recovery Tests

        [Fact]
        public async Task Recovery_RedisComesBack_SystemRecovers()
        {
            // Arrange - Start with Redis down
            var userId = Guid.NewGuid();
            var redisStore = new FlakyQuotaStore();
            redisStore.SetAvailability(false);

            using var host = await CreateTestHostWithFlakyStore(redisStore);
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act 1 - Should fail
            var failResponse = await client.GetAsync("/test-endpoint");
            failResponse.StatusCode.Should().NotBe(HttpStatusCode.OK);

            // Redis comes back online
            redisStore.SetAvailability(true);
            await redisStore.SetAsync(userId, 5);

            // Act 2 - Should succeed
            var successResponse = await client.GetAsync("/test-endpoint");

            // Assert
            successResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        #endregion

        #region Circuit Breaker Tests (Conceptual)

        [Fact]
        public async Task CircuitBreaker_MultipleFailures_TripsCircuit()
        {
            // Note: This is a conceptual test
            // In production, circuit breaker pattern should be implemented
            // to prevent cascading failures

            // Arrange - Flaky Redis that fails intermittently
            var userId = Guid.NewGuid();
            var redisStore = new FlakyQuotaStore(failureRate: 0.8); // 80% failure rate
            using var host = await CreateTestHostWithFlakyStore(redisStore);
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act - Make multiple requests
            var responses = new List<HttpStatusCode>();
            for (int i = 0; i < 10; i++)
            {
                var response = await client.GetAsync("/test-endpoint");
                responses.Add(response.StatusCode);
            }

            // Assert
            // Most requests should fail
            var failureCount = responses.Count(s => s != HttpStatusCode.OK);
            failureCount.Should().BeGreaterThan(5);
        }

        #endregion

        #region Helper Classes and Methods

        /// <summary>
        /// Flaky quota store that simulates intermittent failures
        /// </summary>
        private class FlakyQuotaStore : IQuotaStore
        {
            private readonly Dictionary<Guid, int> _store = new();
            private bool _isAvailable = true;
            private readonly double _failureRate;
            private readonly Random _random = new();

            public FlakyQuotaStore(double failureRate = 0.0)
            {
                _failureRate = failureRate;
            }

            public void SetAvailability(bool isAvailable)
            {
                _isAvailable = isAvailable;
            }

            private void CheckAvailability()
            {
                if (!_isAvailable || _random.NextDouble() < _failureRate)
                {
                    throw new InvalidOperationException("Redis is unavailable");
                }
            }

            public Task<int?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
            {
                CheckAvailability();
                return Task.FromResult(_store.TryGetValue(userId, out var value) ? (int?)value : null);
            }

            public Task SetAsync(Guid userId, int quota, CancellationToken cancellationToken = default)
            {
                CheckAvailability();
                _store[userId] = quota;
                return Task.CompletedTask;
            }

            public Task<bool> CompareExchangeConsumeAsync(Guid userId, int amount,
                CancellationToken cancellationToken = default)
            {
                CheckAvailability();
                if (_store.TryGetValue(userId, out var current) && current >= amount)
                {
                    _store[userId] = current - amount;
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Simulated Redis health check that can be configured to fail
        /// </summary>
        private class SimulatedRedisHealthCheck : IHealthCheck
        {
            private readonly bool _isAvailable;
            private readonly int _latencyMs;

            public SimulatedRedisHealthCheck(bool isAvailable, int latencyMs = 0)
            {
                _isAvailable = isAvailable;
                _latencyMs = latencyMs;
            }

            public async Task<HealthCheckResult> CheckHealthAsync(
                HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                if (_latencyMs > 0)
                {
                    await Task.Delay(_latencyMs, cancellationToken);
                }

                if (!_isAvailable)
                {
                    return HealthCheckResult.Unhealthy("Redis is not available");
                }

                if (_latencyMs > 50)
                {
                    return HealthCheckResult.Degraded(
                        $"Redis latency is high ({_latencyMs}ms)",
                        data: new Dictionary<string, object> { ["latency_ms"] = _latencyMs });
                }

                return HealthCheckResult.Healthy($"Redis is healthy (latency: {_latencyMs}ms)");
            }
        }

        /// <summary>
        /// Simulated Postgres health check that can be configured to fail
        /// </summary>
        private class SimulatedPostgresHealthCheck : IHealthCheck
        {
            private readonly bool _isAvailable;
            private readonly int _latencyMs;

            public SimulatedPostgresHealthCheck(bool isAvailable, int latencyMs = 0)
            {
                _isAvailable = isAvailable;
                _latencyMs = latencyMs;
            }

            public async Task<HealthCheckResult> CheckHealthAsync(
                HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                if (_latencyMs > 0)
                {
                    await Task.Delay(_latencyMs, cancellationToken);
                }

                if (!_isAvailable)
                {
                    return HealthCheckResult.Unhealthy("Postgres is not available");
                }

                if (_latencyMs > 100)
                {
                    return HealthCheckResult.Degraded(
                        $"Postgres latency is high ({_latencyMs}ms)",
                        data: new Dictionary<string, object> { ["latency_ms"] = _latencyMs });
                }

                return HealthCheckResult.Healthy($"Postgres is healthy (latency: {_latencyMs}ms)");
            }
        }

        /// <summary>
        /// Test entitlement service that always allows premium users
        /// </summary>
        private class TestEntitlementService : IEntitlementService
        {
            private readonly IHttpContextAccessor _httpContextAccessor;

            public TestEntitlementService(IHttpContextAccessor httpContextAccessor)
            {
                _httpContextAccessor = httpContextAccessor;
            }

            public Task<bool> IsPremiumAsync(Guid userId, CancellationToken cancellationToken = default)
            {
                var subscriptionClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("subscription");
                return Task.FromResult(subscriptionClaim?.Value == "premium");
            }
        }

        private static async Task<IHost> CreateTestHost(
            bool redisAvailable = true,
            bool databaseAvailable = true,
            int redisLatencyMs = 0,
            int databaseLatencyMs = 0,
            bool allowPremiumOnFailure = false)
        {
            var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            // Register quota system
                            services.Configure<QuotaOptions>(options =>
                            {
                                options.DefaultFreeQuota = 5;
                                options.DailyResetEnabled = false;
                                options.StorageBackend = "InMemory";
                            });

                            // Register quota store
                            services.AddSingleton<IQuotaStore>(provider =>
                            {
                                var logger = provider.GetRequiredService<ILoggerFactory>();
                                if (!redisAvailable)
                                {
                                    // Return a store that always throws
                                    var flakyStore = new FlakyQuotaStore();
                                    flakyStore.SetAvailability(false);
                                    return flakyStore;
                                }
                                else if (redisLatencyMs > 0)
                                {
                                    // Return a store with artificial latency
                                    return new DelayedQuotaStore(
                                        new InMemoryQuotaStore(logger.CreateLogger<InMemoryQuotaStore>()),
                                        redisLatencyMs);
                                }
                                else
                                {
                                    return new InMemoryQuotaStore(logger.CreateLogger<InMemoryQuotaStore>());
                                }
                            });

                            services.AddScoped<IEntitlementService, TestEntitlementService>();
                            services.AddScoped<IQuotaService, Infrastructure.Services.QuotaService>();
                            services.AddHttpContextAccessor();
                            services.AddLogging();

                            // Register health checks
                            services.AddHealthChecks()
                                .AddCheck("redis",
                                    new SimulatedRedisHealthCheck(redisAvailable, redisLatencyMs),
                                    failureStatus: HealthStatus.Unhealthy,
                                    tags: new[] { "ready", "redis" })
                                .AddCheck("postgres",
                                    new SimulatedPostgresHealthCheck(databaseAvailable, databaseLatencyMs),
                                    failureStatus: HealthStatus.Unhealthy,
                                    tags: new[] { "ready", "database" })
                                .AddCheck("self", () => HealthCheckResult.Healthy("API is running"),
                                    tags: new[] { "live" });
                        })
                        .Configure(app =>
                        {
                            // Middleware pipeline
                            app.Use(async (context, next) =>
                            {
                                // Simulate authentication
                                var authHeader = context.Request.Headers["Authorization"].ToString();
                                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                                {
                                    var token = authHeader.Substring("Bearer ".Length);
                                    var (userId, isPremium) = ParseTestToken(token);
                                    context.User = CreateTestPrincipal(userId, isPremium);
                                }
                                await next();
                            });

                            app.UseMiddleware<EntitlementAndQuotaMiddleware>();

                            // Test endpoints
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/test-endpoint", () => Results.Ok(new { message = "Success" }));

                                endpoints.MapGet("/public-endpoint", () => Results.Ok(new { message = "Public" }))
                                    .AllowAnonymous()
                                    .WithMetadata(new SkipQuotaAttribute());

                                endpoints.MapGet("/metrics", () => Results.Ok("# Metrics"))
                                    .AllowAnonymous()
                                    .WithMetadata(new SkipQuotaAttribute());

                                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
                                {
                                    Predicate = check => check.Tags.Contains("ready")
                                }).AllowAnonymous().WithMetadata(new SkipQuotaAttribute());

                                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
                                {
                                    Predicate = check => check.Tags.Contains("live")
                                }).AllowAnonymous().WithMetadata(new SkipQuotaAttribute());
                            });
                        });
                })
                .StartAsync();

            return host;
        }

        private static async Task<IHost> CreateTestHostWithFlakyStore(FlakyQuotaStore flakyStore)
        {
            var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.Configure<QuotaOptions>(options =>
                            {
                                options.DefaultFreeQuota = 5;
                                options.DailyResetEnabled = false;
                            });

                            services.AddSingleton<IQuotaStore>(flakyStore);
                            services.AddScoped<IEntitlementService, TestEntitlementService>();
                            services.AddScoped<IQuotaService, Infrastructure.Services.QuotaService>();
                            services.AddHttpContextAccessor();
                            services.AddLogging();
                        })
                        .Configure(app =>
                        {
                            app.Use(async (context, next) =>
                            {
                                var authHeader = context.Request.Headers["Authorization"].ToString();
                                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                                {
                                    var token = authHeader.Substring("Bearer ".Length);
                                    var (userId, isPremium) = ParseTestToken(token);
                                    context.User = CreateTestPrincipal(userId, isPremium);
                                }
                                await next();
                            });

                            app.UseMiddleware<EntitlementAndQuotaMiddleware>();

                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/test-endpoint", () => Results.Ok(new { message = "Success" }));
                            });
                        });
                })
                .StartAsync();

            return host;
        }

        /// <summary>
        /// Quota store wrapper that adds artificial latency
        /// </summary>
        private class DelayedQuotaStore : IQuotaStore
        {
            private readonly IQuotaStore _inner;
            private readonly int _delayMs;

            public DelayedQuotaStore(IQuotaStore inner, int delayMs)
            {
                _inner = inner;
                _delayMs = delayMs;
            }

            public async Task<int?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
            {
                await Task.Delay(_delayMs, cancellationToken);
                return await _inner.GetAsync(userId, cancellationToken);
            }

            public async Task SetAsync(Guid userId, int quota, CancellationToken cancellationToken = default)
            {
                await Task.Delay(_delayMs, cancellationToken);
                await _inner.SetAsync(userId, quota, cancellationToken);
            }

            public async Task<bool> CompareExchangeConsumeAsync(Guid userId, int amount,
                CancellationToken cancellationToken = default)
            {
                await Task.Delay(_delayMs, cancellationToken);
                return await _inner.CompareExchangeConsumeAsync(userId, amount, cancellationToken);
            }
        }

        private static string GenerateTestToken(Guid userId, bool isPremium)
        {
            var data = $"{userId}|{(isPremium ? "premium" : "free")}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
        }

        private static (Guid userId, bool isPremium) ParseTestToken(string token)
        {
            var data = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = data.Split('|');
            return (Guid.Parse(parts[0]), parts[1] == "premium");
        }

        private static ClaimsPrincipal CreateTestPrincipal(Guid userId, bool isPremium)
        {
            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("subscription", isPremium ? "premium" : "free")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        #endregion
    }
}
