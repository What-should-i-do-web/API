using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;
using System.Threading.Tasks;

namespace WhatShouldIDo.Tests
{
    public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public HealthCheckTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task HealthCheck_ReturnsSuccessAndCorrectContentType()
        {
            // Act
            var response = await _client.GetAsync("/api/health");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString() ?? "");
        }

        [Fact]
        public async Task ContextTimeRecommendations_ReturnsValidResponse()
        {
            // Act
            var response = await _client.GetAsync("/api/context/time-recommendations");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("currentTime", out _));
            Assert.True(root.TryGetProperty("timeContext", out _));
            Assert.True(root.TryGetProperty("recommendations", out _));
        }

        [Fact]
        public async Task DiscoverEndpoint_WithoutAuth_ReturnsFallbackSuggestions()
        {
            // Act
            var response = await _client.GetAsync("/api/discover?lat=41.0082&lng=28.9784&radius=3000");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("personalized", out var personalizedProp));
            Assert.False(personalizedProp.GetBoolean()); // Should be false for unauthenticated users

            Assert.True(root.TryGetProperty("suggestions", out var suggestionsProp));
            Assert.True(suggestionsProp.GetArrayLength() > 0);
        }

        [Fact]
        public async Task ContextInsights_WithValidCoordinates_ReturnsContextualData()
        {
            // Act - Test with Istanbul coordinates
            var response = await _client.GetAsync("/api/context/insights?lat=41.0082&lng=28.9784");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("location", out _));
            Assert.True(root.TryGetProperty("timeContext", out _));
            Assert.True(root.TryGetProperty("season", out _));
            Assert.True(root.TryGetProperty("locationContext", out _));
            Assert.True(root.TryGetProperty("weather", out _));
            Assert.True(root.TryGetProperty("recommendations", out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        [InlineData("999")]
        [InlineData("-999")]
        public async Task DiscoverEndpoint_WithInvalidCoordinates_HandleGracefully(string invalidCoord)
        {
            // Act
            var response = await _client.GetAsync($"/api/discover?lat={invalidCoord}&lng={invalidCoord}&radius=3000");

            // Assert
            // Should either return 400 (Bad Request) or handle gracefully with defaults
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest || 
                       response.StatusCode == HttpStatusCode.OK);
        }

        [Fact]
        public async Task SwaggerUI_IsAccessibleInDevelopment()
        {
            // Act
            var response = await _client.GetAsync("/swagger");

            // Assert
            // Should redirect to swagger/index.html or return the Swagger UI page
            Assert.True(response.StatusCode == HttpStatusCode.OK || 
                       response.StatusCode == HttpStatusCode.Redirect ||
                       response.StatusCode == HttpStatusCode.MovedPermanently);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}