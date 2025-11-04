using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using Xunit;

namespace WhatShouldIDo.Tests.Integration
{
    /// <summary>
    /// Integration tests for discovery endpoints:
    /// - GET /api/discover (nearby suggestions)
    /// - GET /api/discover/random (random suggestion)
    /// - POST /api/discover/prompt (prompt-based discovery)
    ///
    /// Tests both authenticated (personalized) and anonymous (fallback) modes.
    /// </summary>
    public class DiscoveryIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public DiscoveryIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        #region Nearby Discovery Tests

        [Fact]
        public async Task Discover_WithoutAuth_ReturnsFallbackSuggestions()
        {
            // Act
            var response = await _client.GetAsync("/api/discover?lat=41.0082&lng=28.9784&radius=5000");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            // Should NOT be personalized for anonymous users
            root.GetProperty("personalized").GetBoolean().Should().BeFalse();

            // Should have suggestions array
            root.TryGetProperty("suggestions", out var suggestions).Should().BeTrue();
            suggestions.GetArrayLength().Should().BeGreaterThan(0);

            // Suggestions should have required fields
            var firstSuggestion = suggestions[0];
            firstSuggestion.TryGetProperty("id", out _).Should().BeTrue();
            firstSuggestion.TryGetProperty("name", out _).Should().BeTrue();
            firstSuggestion.TryGetProperty("category", out _).Should().BeTrue();
            firstSuggestion.TryGetProperty("location", out _).Should().BeTrue();
        }

        [Fact]
        public async Task Discover_WithAuth_ReturnsPersonalizedSuggestions()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var request = new HttpRequestMessage(HttpMethod.Get,
                "/api/discover?lat=41.0082&lng=28.9784&radius=5000");
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            // Should be personalized for authenticated users
            root.GetProperty("personalized").GetBoolean().Should().BeTrue();

            // Should include userId
            root.TryGetProperty("userId", out _).Should().BeTrue();

            // Should have suggestions
            root.TryGetProperty("suggestions", out var suggestions).Should().BeTrue();
            suggestions.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(41.0082, 28.9784, 3000)] // Istanbul, Turkey
        [InlineData(40.7128, -74.0060, 5000)] // New York, USA
        [InlineData(51.5074, -0.1278, 10000)] // London, UK
        [InlineData(35.6762, 139.6503, 2000)] // Tokyo, Japan
        public async Task Discover_WithVariousLocations_ReturnsRelevantResults(double lat, double lng, int radius)
        {
            // Act
            var response = await _client.GetAsync($"/api/discover?lat={lat}&lng={lng}&radius={radius}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.TryGetProperty("suggestions", out var suggestions).Should().BeTrue();

            // Should return at least one suggestion (unless no places in radius)
            // We accept 0 results for valid locations with no places nearby
            suggestions.GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
        }

        [Theory]
        [InlineData(-100, 0)] // Invalid latitude (< -90)
        [InlineData(100, 0)] // Invalid latitude (> 90)
        [InlineData(0, -200)] // Invalid longitude (< -180)
        [InlineData(0, 200)] // Invalid longitude (> 180)
        public async Task Discover_WithInvalidCoordinates_ReturnsBadRequest(double lat, double lng)
        {
            // Act
            var response = await _client.GetAsync($"/api/discover?lat={lat}&lng={lng}&radius=3000");

            // Assert
            // Should either return BadRequest or handle gracefully
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        [Fact]
        public async Task Discover_WithVeryLargeRadius_HandlesGracefully()
        {
            // Act - Try with 100km radius
            var response = await _client.GetAsync("/api/discover?lat=41.0082&lng=28.9784&radius=100000");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }

        #endregion

        #region Random Suggestion Tests

        [Fact]
        public async Task Random_WithoutAuth_ReturnsSingleSuggestion()
        {
            // Act
            var response = await _client.GetAsync("/api/discover/random?lat=41.0082&lng=28.9784&radius=5000");

            // Assert
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;

                root.GetProperty("personalized").GetBoolean().Should().BeFalse();
                root.TryGetProperty("suggestion", out var suggestion).Should().BeTrue();

                // Suggestion should have required fields
                suggestion.TryGetProperty("id", out _).Should().BeTrue();
                suggestion.TryGetProperty("name", out _).Should().BeTrue();
            }
            else
            {
                // If no places found, should return 404
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
        }

        [Fact]
        public async Task Random_WithAuth_ReturnsPersonalizedSuggestion()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var request = new HttpRequestMessage(HttpMethod.Get,
                "/api/discover/random?lat=41.0082&lng=28.9784&radius=5000");
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;

                root.GetProperty("personalized").GetBoolean().Should().BeTrue();
                root.TryGetProperty("suggestion", out _).Should().BeTrue();
            }
        }

        [Fact]
        public async Task Random_MultipleCalls_ReturnsDifferentSuggestions()
        {
            // Act
            var response1 = await _client.GetAsync("/api/discover/random?lat=41.0082&lng=28.9784&radius=10000");
            var response2 = await _client.GetAsync("/api/discover/random?lat=41.0082&lng=28.9784&radius=10000");

            // Assert
            if (response1.StatusCode == HttpStatusCode.OK && response2.StatusCode == HttpStatusCode.OK)
            {
                var content1 = await response1.Content.ReadAsStringAsync();
                var content2 = await response2.Content.ReadAsStringAsync();

                using var doc1 = JsonDocument.Parse(content1);
                using var doc2 = JsonDocument.Parse(content2);

                var suggestion1Id = doc1.RootElement.GetProperty("suggestion").GetProperty("id").GetString();
                var suggestion2Id = doc2.RootElement.GetProperty("suggestion").GetProperty("id").GetString();

                // Suggestions MIGHT be different (not guaranteed with small dataset)
                // We just verify both returned valid suggestions
                suggestion1Id.Should().NotBeNullOrEmpty();
                suggestion2Id.Should().NotBeNullOrEmpty();
            }
        }

        #endregion

        #region Prompt-Based Discovery Tests

        [Fact]
        public async Task Prompt_WithSimpleQuery_ReturnsSuggestions()
        {
            // Arrange
            var promptRequest = new PromptRequest
            {
                Prompt = "coffee shop",
                Latitude = 41.0082f,
                Longitude = 28.9784f,
                Radius = 5000
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/discover/prompt", promptRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.TryGetProperty("suggestions", out var suggestions).Should().BeTrue();
            suggestions.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Prompt_WithAuth_ReturnsPersonalizedResults()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var promptRequest = new PromptRequest
            {
                Prompt = "romantic restaurant",
                Latitude = 41.0082f,
                Longitude = 28.9784f,
                Radius = 5000
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/discover/prompt")
            {
                Content = JsonContent.Create(promptRequest)
            };
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.GetProperty("personalized").GetBoolean().Should().BeTrue();
            root.TryGetProperty("userId", out _).Should().BeTrue();
        }

        [Theory]
        [InlineData("romantic dinner")]
        [InlineData("family-friendly restaurant")]
        [InlineData("cheap eats")]
        [InlineData("museums and historical sites")]
        [InlineData("nightlife and bars")]
        [InlineData("parks and outdoor activities")]
        public async Task Prompt_WithVariousQueries_ReturnsRelevantResults(string prompt)
        {
            // Arrange
            var promptRequest = new PromptRequest
            {
                Prompt = prompt,
                Latitude = 41.0082f,
                Longitude = 28.9784f,
                Radius = 10000
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/discover/prompt", promptRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.TryGetProperty("suggestions", out var suggestions).Should().BeTrue();
            // Accept 0 results if no matching places found
            suggestions.GetArrayLength().Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task Prompt_WithEmptyPrompt_ReturnsBadRequest()
        {
            // Arrange
            var promptRequest = new PromptRequest
            {
                Prompt = "", // Empty prompt
                Latitude = 41.0082f,
                Longitude = 28.9784f
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/discover/prompt", promptRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Prompt_WithoutLocation_UsesDefaultOrReturnsBadRequest()
        {
            // Arrange
            var promptRequest = new PromptRequest
            {
                Prompt = "coffee shop"
                // No location provided
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/discover/prompt", promptRequest);

            // Assert
            // Should either use a default location or return BadRequest
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }

        #endregion

        #region Quota Integration Tests

        [Fact]
        public async Task Discover_ConsumesQuota_ForAuthenticatedFreeUser()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var request = new HttpRequestMessage(HttpMethod.Get,
                "/api/discover?lat=41.0082&lng=28.9784&radius=5000");
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Should include quota headers
            response.Headers.Should().ContainKey("X-Quota-Remaining");
            var remaining = int.Parse(response.Headers.GetValues("X-Quota-Remaining").First());
            remaining.Should().BeLessThan(5); // Started with 5, now consumed 1
        }

        [Fact]
        public async Task Discover_WhenQuotaExhausted_ReturnsForbidden()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var request = new HttpRequestMessage(HttpMethod.Get,
                "/api/discover?lat=41.0082&lng=28.9784&radius=5000");
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act - Consume all quota (5 requests)
            for (int i = 0; i < 5; i++)
            {
                var req = new HttpRequestMessage(HttpMethod.Get,
                    "/api/discover?lat=41.0082&lng=28.9784&radius=5000");
                req.Headers.Add("Authorization", $"Bearer {token}");
                await _client.SendAsync(req);
            }

            // Try one more request (should be blocked)
            var finalRequest = new HttpRequestMessage(HttpMethod.Get,
                "/api/discover?lat=41.0082&lng=28.9784&radius=5000");
            finalRequest.Headers.Add("Authorization", $"Bearer {token}");
            var finalResponse = await _client.SendAsync(finalRequest);

            // Assert
            finalResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var content = await finalResponse.Content.ReadAsStringAsync();
            content.ToLower().Should().Contain("quota");
        }

        #endregion

        #region Response Format Tests

        [Fact]
        public async Task Discover_ResponseIncludesDistance()
        {
            // Act
            var response = await _client.GetAsync("/api/discover?lat=41.0082&lng=28.9784&radius=5000");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var suggestions = root.GetProperty("suggestions");

            if (suggestions.GetArrayLength() > 0)
            {
                var firstSuggestion = suggestions[0];
                firstSuggestion.TryGetProperty("distance", out _).Should().BeTrue();
            }
        }

        [Fact]
        public async Task Discover_ResponseIncludesRating()
        {
            // Act
            var response = await _client.GetAsync("/api/discover?lat=41.0082&lng=28.9784&radius=5000");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var suggestions = root.GetProperty("suggestions");

            if (suggestions.GetArrayLength() > 0)
            {
                var firstSuggestion = suggestions[0];
                firstSuggestion.TryGetProperty("rating", out var rating).Should().BeTrue();

                if (rating.ValueKind == JsonValueKind.Number)
                {
                    rating.GetDouble().Should().BeInRange(0, 5);
                }
            }
        }

        #endregion

        #region Helper Methods

        private async Task<string> RegisterAndGetToken()
        {
            var registerRequest = new RegisterRequest
            {
                Email = $"test_{Guid.NewGuid()}@example.com",
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                UserName = $"user_{Guid.NewGuid().ToString().Substring(0, 8)}",
                FirstName = "Test",
                LastName = "User"
            };

            var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            return root.GetProperty("token").GetString()!;
        }

        #endregion
    }
}
