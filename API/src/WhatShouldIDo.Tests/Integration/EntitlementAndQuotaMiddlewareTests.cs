using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WhatShouldIDo.API.Attributes;
using WhatShouldIDo.API.Middleware;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Quota;
using Xunit;

namespace WhatShouldIDo.Tests.Integration
{
    public class EntitlementAndQuotaMiddlewareTests
    {
        [Fact]
        public async Task AnonymousRequest_ReturnsUnauthorized()
        {
            // Arrange
            using var host = await CreateTestHost();
            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Authentication is required");
        }

        [Fact]
        public async Task AllowAnonymousEndpoint_AllowsAnonymousAccess()
        {
            // Arrange
            using var host = await CreateTestHost();
            var client = host.GetTestClient();

            // Act
            var response = await client.GetAsync("/public-endpoint");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SkipQuotaEndpoint_BypassesQuotaCheck()
        {
            // Arrange
            using var host = await CreateTestHost();
            var client = host.GetTestClient();
            var token = GenerateTestToken(Guid.NewGuid(), isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act - call multiple times, should never hit quota
            for (int i = 0; i < 10; i++)
            {
                var response = await client.GetAsync("/skip-quota-endpoint");
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }

        [Fact]
        public async Task AuthenticatedFreeUser_WithCredits_ConsumesQuotaAndSucceeds()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Initialize quota
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 5);

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.Should().ContainKey("X-Quota-Remaining");
            var remaining = int.Parse(response.Headers.GetValues("X-Quota-Remaining").First());
            remaining.Should().Be(4);
        }

        [Fact]
        public async Task AuthenticatedFreeUser_ExhaustedQuota_ReturnsForbidden()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Initialize quota to 0
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 0);

            // Act
            var response = await client.GetAsync("/test-endpoint");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Quota exhausted");
            content.Should().Contain("Upgrade to premium");
        }

        [Fact]
        public async Task AuthenticatedPremiumUser_UnlimitedAccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: true);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act - call multiple times beyond free quota
            for (int i = 0; i < 10; i++)
            {
                var response = await client.GetAsync("/test-endpoint");
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }

            // Assert - quota should never be consumed for premium
            var store = host.Services.GetRequiredService<IQuotaStore>();
            var quota = await store.GetAsync(userId);
            quota.Should().BeNull(); // Premium users don't have quota tracking
        }

        [Fact]
        public async Task PremiumOnlyEndpoint_NonPremiumUser_ReturnsForbidden()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/premium-only-endpoint");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Premium Subscription Required");
        }

        [Fact]
        public async Task PremiumOnlyEndpoint_PremiumUser_Succeeds()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: true);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await client.GetAsync("/premium-only-endpoint");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task FreeUser_ConsumeAll5Credits_ThenBlocked()
        {
            // Arrange
            var userId = Guid.NewGuid();
            using var host = await CreateTestHost();
            var client = host.GetTestClient();
            var token = GenerateTestToken(userId, isPremium: false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Initialize with 5 credits
            var store = host.Services.GetRequiredService<IQuotaStore>();
            await store.SetAsync(userId, 5);

            // Act - consume all 5 credits
            for (int i = 0; i < 5; i++)
            {
                var response = await client.GetAsync("/test-endpoint");
                response.StatusCode.Should().Be(HttpStatusCode.OK, $"Request {i + 1} should succeed");
            }

            // Act - 6th request should be blocked
            var blockedResponse = await client.GetAsync("/test-endpoint");

            // Assert
            blockedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var content = await blockedResponse.Content.ReadAsStringAsync();
            content.Should().Contain("Quota exhausted");
        }

        // Helper methods

        private static async Task<IHost> CreateTestHost()
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
                            // Middleware pipeline
                            app.Use(async (context, next) =>
                            {
                                // Simulate authentication - extract token and set user
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

                                endpoints.MapGet("/skip-quota-endpoint", () => Results.Ok(new { message = "No Quota" }))
                                    .WithMetadata(new SkipQuotaAttribute());

                                endpoints.MapGet("/premium-only-endpoint", () => Results.Ok(new { message = "Premium" }))
                                    .WithMetadata(new PremiumOnlyAttribute());
                            });
                        });
                })
                .StartAsync();

            return host;
        }

        private static string GenerateTestToken(Guid userId, bool isPremium)
        {
            // Simple encoding for test purposes
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

        // Test implementation of IEntitlementService that reads from HttpContext
        private class TestEntitlementService : IEntitlementService
        {
            private readonly IHttpContextAccessor _httpContextAccessor;

            public TestEntitlementService(IHttpContextAccessor httpContextAccessor)
            {
                _httpContextAccessor = httpContextAccessor;
            }

            public Task<bool> IsPremiumAsync(Guid userId, System.Threading.CancellationToken cancellationToken = default)
            {
                var subscriptionClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("subscription");
                return Task.FromResult(subscriptionClaim?.Value == "premium");
            }
        }
    }
}
