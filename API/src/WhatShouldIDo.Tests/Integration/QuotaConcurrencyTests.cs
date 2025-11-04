using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using Xunit.Abstractions;

namespace WhatShouldIDo.Tests.Integration
{
    /// <summary>
    /// Concurrency tests for quota system to verify thread-safety and atomicity.
    ///
    /// Tests cover:
    /// - 100 parallel requests from same free user (only 5 should succeed)
    /// - Race condition detection
    /// - Atomic operation verification
    /// - Premium user unlimited concurrent access
    /// - Multiple users concurrent access
    /// - Quota consumption under load
    /// </summary>
    public class QuotaConcurrencyTests
    {
        private readonly ITestOutputHelper _output;

        public QuotaConcurrencyTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Core Concurrency Tests

        [Fact]
        public async Task Concurrent_100Requests_FreeUser_Only5Succeed()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 5); // Set initial quota to 5

            var token = GenerateTestToken(userId, isPremium: false);

            // Act - Launch 100 parallel requests
            var tasks = new List<Task<HttpStatusCode>>();
            var barrier = new Barrier(100); // Synchronize all tasks to start at the same time

            for (int i = 0; i < 100; i++)
            {
                var taskId = i; // Capture for logging
                tasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    // Wait for all tasks to be ready
                    barrier.SignalAndWait();

                    // Execute request
                    var sw = Stopwatch.StartNew();
                    var response = await client.GetAsync("/test-endpoint");
                    sw.Stop();

                    _output.WriteLine($"Task {taskId}: {response.StatusCode} in {sw.ElapsedMilliseconds}ms");
                    return response.StatusCode;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(s => s == HttpStatusCode.OK);
            var forbiddenCount = results.Count(s => s == HttpStatusCode.Forbidden);

            _output.WriteLine($"Success: {successCount}, Forbidden: {forbiddenCount}");

            // Exactly 5 should succeed, 95 should be forbidden
            successCount.Should().Be(5, "Free user with 5 quota should succeed exactly 5 times");
            forbiddenCount.Should().Be(95, "95 requests should be blocked due to quota exhaustion");

            // Verify final quota is 0
            var finalQuota = await store.GetAsync(userId);
            finalQuota.Should().Be(0, "All quota should be consumed");
        }

        [Fact]
        public async Task Concurrent_50Requests_FreeUser_With10Quota_Only10Succeed()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 10); // Set initial quota to 10

            var token = GenerateTestToken(userId, isPremium: false);

            // Act - Launch 50 parallel requests
            var tasks = new List<Task<HttpStatusCode>>();
            var barrier = new Barrier(50);

            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    barrier.SignalAndWait();
                    var response = await client.GetAsync("/test-endpoint");
                    return response.StatusCode;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(s => s == HttpStatusCode.OK);
            successCount.Should().Be(10, "Should consume exactly 10 quota");

            var finalQuota = await store.GetAsync(userId);
            finalQuota.Should().Be(0);
        }

        [Fact]
        public async Task Concurrent_PremiumUser_AllRequestsSucceed()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var token = GenerateTestToken(userId, isPremium: true);

            // Act - Launch 100 parallel requests (premium user)
            var tasks = new List<Task<HttpStatusCode>>();
            var barrier = new Barrier(100);

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    barrier.SignalAndWait();
                    var response = await client.GetAsync("/test-endpoint");
                    return response.StatusCode;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(s => s == HttpStatusCode.OK);
            successCount.Should().Be(100, "Premium user should have unlimited concurrent access");
        }

        #endregion

        #region Multiple Users Concurrency Tests

        [Fact]
        public async Task Concurrent_MultipleUsers_IndependentQuotas()
        {
            // Arrange
            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();
            var user3 = Guid.NewGuid();

            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();

            // Each user gets 5 quota
            await store.SetAsync(user1, 5);
            await store.SetAsync(user2, 5);
            await store.SetAsync(user3, 5);

            var token1 = GenerateTestToken(user1, isPremium: false);
            var token2 = GenerateTestToken(user2, isPremium: false);
            var token3 = GenerateTestToken(user3, isPremium: false);

            // Act - 30 requests per user (90 total)
            var allTasks = new List<Task<(Guid userId, HttpStatusCode status)>>();

            for (int i = 0; i < 30; i++)
            {
                // User 1
                allTasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
                    var response = await client.GetAsync("/test-endpoint");
                    return (user1, response.StatusCode);
                }));

                // User 2
                allTasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
                    var response = await client.GetAsync("/test-endpoint");
                    return (user2, response.StatusCode);
                }));

                // User 3
                allTasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token3);
                    var response = await client.GetAsync("/test-endpoint");
                    return (user3, response.StatusCode);
                }));
            }

            var results = await Task.WhenAll(allTasks);

            // Assert - Each user should have exactly 5 successes
            var user1Successes = results.Count(r => r.userId == user1 && r.status == HttpStatusCode.OK);
            var user2Successes = results.Count(r => r.userId == user2 && r.status == HttpStatusCode.OK);
            var user3Successes = results.Count(r => r.userId == user3 && r.status == HttpStatusCode.OK);

            _output.WriteLine($"User1: {user1Successes}, User2: {user2Successes}, User3: {user3Successes}");

            user1Successes.Should().Be(5, "User 1 should consume exactly 5 quota");
            user2Successes.Should().Be(5, "User 2 should consume exactly 5 quota");
            user3Successes.Should().Be(5, "User 3 should consume exactly 5 quota");

            // Total successes should be 15
            var totalSuccesses = results.Count(r => r.status == HttpStatusCode.OK);
            totalSuccesses.Should().Be(15);
        }

        [Fact]
        public async Task Concurrent_MixedUserTypes_CorrectBehavior()
        {
            // Arrange
            var freeUser = Guid.NewGuid();
            var premiumUser = Guid.NewGuid();

            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(freeUser, 5);

            var freeToken = GenerateTestToken(freeUser, isPremium: false);
            var premiumToken = GenerateTestToken(premiumUser, isPremium: true);

            // Act - 50 requests from each user
            var allTasks = new List<Task<(bool isPremium, HttpStatusCode status)>>();

            for (int i = 0; i < 50; i++)
            {
                // Free user request
                allTasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", freeToken);
                    var response = await client.GetAsync("/test-endpoint");
                    return (false, response.StatusCode);
                }));

                // Premium user request
                allTasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", premiumToken);
                    var response = await client.GetAsync("/test-endpoint");
                    return (true, response.StatusCode);
                }));
            }

            var results = await Task.WhenAll(allTasks);

            // Assert
            var freeSuccesses = results.Count(r => !r.isPremium && r.status == HttpStatusCode.OK);
            var premiumSuccesses = results.Count(r => r.isPremium && r.status == HttpStatusCode.OK);

            _output.WriteLine($"Free: {freeSuccesses}, Premium: {premiumSuccesses}");

            freeSuccesses.Should().Be(5, "Free user should succeed exactly 5 times");
            premiumSuccesses.Should().Be(50, "Premium user should succeed all 50 times");
        }

        #endregion

        #region Race Condition Tests

        [Fact]
        public async Task RaceCondition_NoDoubleSpending()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 1); // Only 1 quota available

            var token = GenerateTestToken(userId, isPremium: false);

            // Act - Launch 10 parallel requests fighting for 1 quota
            var tasks = new List<Task<HttpStatusCode>>();
            var barrier = new Barrier(10);

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    barrier.SignalAndWait(); // All start at once
                    var response = await client.GetAsync("/test-endpoint");
                    return response.StatusCode;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(s => s == HttpStatusCode.OK);
            successCount.Should().Be(1, "Only 1 request should succeed (no double-spending)");

            var finalQuota = await store.GetAsync(userId);
            finalQuota.Should().Be(0, "Quota should be exactly 0");
        }

        [Fact]
        public async Task RaceCondition_AtomicDecrements()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();

            // Start with 20 quota
            await store.SetAsync(userId, 20);

            var token = GenerateTestToken(userId, isPremium: false);

            // Act - Launch 100 requests (should consume all 20, then block)
            var tasks = new List<Task<HttpStatusCode>>();

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await client.GetAsync("/test-endpoint");
                    return response.StatusCode;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(s => s == HttpStatusCode.OK);
            successCount.Should().Be(20, "Exactly 20 requests should succeed");

            var finalQuota = await store.GetAsync(userId);
            finalQuota.Should().Be(0, "All quota consumed, no over-consumption");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task Performance_100ConcurrentRequests_CompletesQuickly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 100); // Enough quota for all

            var token = GenerateTestToken(userId, isPremium: false);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<HttpStatusCode>>();

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await client.GetAsync("/test-endpoint");
                    return response.StatusCode;
                }));
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var successCount = results.Count(s => s == HttpStatusCode.OK);
            successCount.Should().Be(100);

            _output.WriteLine($"100 requests completed in {stopwatch.ElapsedMilliseconds}ms");

            // Should complete in reasonable time (< 5 seconds for 100 requests)
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
                "100 concurrent requests should complete within 5 seconds");
        }

        [Fact]
        public async Task Performance_HighContention_NoDeadlock()
        {
            // Arrange - 10 users, each with 5 quota, 500 total requests
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();

            var userTokens = new List<(Guid userId, string token)>();
            for (int i = 0; i < 10; i++)
            {
                var userId = Guid.NewGuid();
                await store.SetAsync(userId, 5);
                userTokens.Add((userId, GenerateTestToken(userId, isPremium: false)));
            }

            // Act - Launch 500 requests (50 per user)
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<HttpStatusCode>>();

            for (int i = 0; i < 50; i++)
            {
                foreach (var (userId, token) in userTokens)
                {
                    var userToken = token; // Capture
                    tasks.Add(Task.Run(async () =>
                    {
                        var client = host.GetTestClient();
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
                        var response = await client.GetAsync("/test-endpoint");
                        return response.StatusCode;
                    }));
                }
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var successCount = results.Count(s => s == HttpStatusCode.OK);
            successCount.Should().Be(50, "10 users * 5 quota = 50 total successes");

            _output.WriteLine($"500 requests (10 users) completed in {stopwatch.ElapsedMilliseconds}ms");

            // Should not deadlock - complete in reasonable time
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000,
                "High contention scenario should complete within 10 seconds");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task EdgeCase_ZeroQuota_AllRequestsBlocked()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 0); // Zero quota

            var token = GenerateTestToken(userId, isPremium: false);

            // Act - Try 20 concurrent requests
            var tasks = new List<Task<HttpStatusCode>>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await client.GetAsync("/test-endpoint");
                    return response.StatusCode;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            var forbiddenCount = results.Count(s => s == HttpStatusCode.Forbidden);
            forbiddenCount.Should().Be(20, "All requests should be blocked with zero quota");
        }

        [Fact]
        public async Task EdgeCase_NegativeQuota_Handled()
        {
            // Arrange - This shouldn't happen, but test defensive programming
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();

            // Try to set negative quota (implementation should handle this)
            await store.SetAsync(userId, -5);

            var token = GenerateTestToken(userId, isPremium: false);

            // Act
            var client = host.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/test-endpoint");

            // Assert - Should be blocked (negative = no quota)
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task EdgeCase_ConcurrentSet_LastWriteWins()
        {
            // Arrange - Test concurrent set operations
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();

            // Act - 50 concurrent sets to different values
            var tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                var value = i;
                tasks.Add(Task.Run(async () =>
                {
                    await store.SetAsync(userId, value);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var finalQuota = await store.GetAsync(userId);
            finalQuota.Should().NotBeNull("Quota should be set to some value");
            // Last write wins - exact value is non-deterministic
        }

        #endregion

        #region Stress Tests

        [Fact]
        public async Task Stress_1000Requests_5Quota_Only5Succeed()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 5);

            var token = GenerateTestToken(userId, isPremium: false);

            // Act - Launch 1000 requests
            var stopwatch = Stopwatch.StartNew();
            var tasks = new List<Task<HttpStatusCode>>();

            for (int i = 0; i < 1000; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var client = host.GetTestClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = await client.GetAsync("/test-endpoint");
                    return response.StatusCode;
                }));
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var successCount = results.Count(s => s == HttpStatusCode.OK);
            successCount.Should().Be(5, "Only 5 out of 1000 should succeed");

            _output.WriteLine($"1000 requests completed in {stopwatch.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Helper Methods

        private static async Task<IHost> CreateTestHost()
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
                                options.StorageBackend = "InMemory";
                            });

                            services.AddSingleton<IQuotaStore>(provider =>
                            {
                                var logger = provider.GetRequiredService<ILoggerFactory>();
                                return new InMemoryQuotaStore(logger.CreateLogger<InMemoryQuotaStore>());
                            });

                            services.AddScoped<IEntitlementService, TestEntitlementService>();
                            services.AddScoped<IQuotaService, Infrastructure.Services.QuotaService>();
                            services.AddHttpContextAccessor();
                            services.AddLogging();
                        })
                        .Configure(app =>
                        {
                            // Authentication middleware
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
