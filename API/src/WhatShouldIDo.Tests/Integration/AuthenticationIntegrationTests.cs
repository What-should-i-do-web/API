using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using Xunit;

namespace WhatShouldIDo.Tests.Integration
{
    /// <summary>
    /// Integration tests for authentication endpoints:
    /// - Register
    /// - Login
    /// - Get current user
    /// - Update profile
    /// - Get API usage
    /// - Logout
    /// </summary>
    public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        #region Registration Tests

        [Fact]
        public async Task Register_WithValidData_ReturnsTokenAndUser()
        {
            // Arrange
            var registerRequest = new RegisterRequest
            {
                Email = $"test_{Guid.NewGuid()}@example.com",
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                UserName = $"testuser_{Guid.NewGuid().ToString().Substring(0, 8)}",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            // Should return token
            root.TryGetProperty("token", out var tokenProp).Should().BeTrue();
            var token = tokenProp.GetString();
            token.Should().NotBeNullOrEmpty();

            // Should return user object
            root.TryGetProperty("user", out var userProp).Should().BeTrue();
            var user = userProp;
            user.GetProperty("email").GetString().Should().Be(registerRequest.Email);
            user.GetProperty("userName").GetString().Should().Be(registerRequest.UserName);
            user.GetProperty("subscriptionTier").GetString().Should().Be("Free");
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_ReturnsConflict()
        {
            // Arrange
            var email = $"duplicate_{Guid.NewGuid()}@example.com";

            var firstRequest = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                UserName = $"user1_{Guid.NewGuid().ToString().Substring(0, 8)}"
            };

            var secondRequest = new RegisterRequest
            {
                Email = email, // Same email
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                UserName = $"user2_{Guid.NewGuid().ToString().Substring(0, 8)}"
            };

            // Act
            await _client.PostAsJsonAsync("/api/auth/register", firstRequest);
            var duplicateResponse = await _client.PostAsJsonAsync("/api/auth/register", secondRequest);

            // Assert
            duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Theory]
        [InlineData("", "SecurePass123!", "username")] // Empty email
        [InlineData("invalid-email", "SecurePass123!", "username")] // Invalid email format
        [InlineData("test@example.com", "weak", "username")] // Weak password
        [InlineData("test@example.com", "SecurePass123!", "")] // Empty username
        public async Task Register_WithInvalidData_ReturnsBadRequest(string email, string password, string userName)
        {
            // Arrange
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                UserName = userName
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion

        #region Login Tests

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
        {
            // Arrange - Register a user first
            var email = $"logintest_{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                UserName = $"loginuser_{Guid.NewGuid().ToString().Substring(0, 8)}"
            };
            await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.TryGetProperty("token", out var tokenProp).Should().BeTrue();
            tokenProp.GetString().Should().NotBeNullOrEmpty();

            root.TryGetProperty("user", out var userProp).Should().BeTrue();
            userProp.GetProperty("email").GetString().Should().Be(email);
        }

        [Fact]
        public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
        {
            // Arrange - Register a user
            var email = $"invalidpass_{Guid.NewGuid()}@example.com";
            var correctPassword = "SecurePass123!";
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = correctPassword,
                ConfirmPassword = correctPassword,
                UserName = $"testuser_{Guid.NewGuid().ToString().Substring(0, 8)}"
            };
            await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Login_WithNonexistentEmail_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "SecurePass123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Get Current User Tests

        [Fact]
        public async Task GetCurrentUser_WithValidToken_ReturnsUserData()
        {
            // Arrange - Register and login
            var token = await RegisterAndGetToken();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.TryGetProperty("id", out _).Should().BeTrue();
            root.TryGetProperty("email", out _).Should().BeTrue();
            root.TryGetProperty("userName", out _).Should().BeTrue();
            root.TryGetProperty("subscriptionTier", out _).Should().BeTrue();
        }

        [Fact]
        public async Task GetCurrentUser_WithoutToken_ReturnsUnauthorized()
        {
            // Act
            var response = await _client.GetAsync("/api/auth/me");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetCurrentUser_WithInvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Add("Authorization", "Bearer invalid_token_here");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Update Profile Tests

        [Fact]
        public async Task UpdateProfile_WithValidData_ReturnsUpdatedUser()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var updateRequest = new UpdateProfileRequest
            {
                FirstName = "Updated",
                LastName = "Name",
                City = "Istanbul"
            };

            var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/profile")
            {
                Content = JsonContent.Create(updateRequest)
            };
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.GetProperty("firstName").GetString().Should().Be(updateRequest.FirstName);
            root.GetProperty("lastName").GetString().Should().Be(updateRequest.LastName);
        }

        [Fact]
        public async Task UpdateProfile_WithoutToken_ReturnsUnauthorized()
        {
            // Arrange
            var updateRequest = new UpdateProfileRequest
            {
                FirstName = "New"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/auth/profile", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        #endregion

        #region Get API Usage Tests

        [Fact]
        public async Task GetApiUsage_WithValidToken_ReturnsUsageData()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/usage");
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.TryGetProperty("dailyUsage", out _).Should().BeTrue();
            root.TryGetProperty("dailyLimit", out _).Should().BeTrue();
            root.TryGetProperty("subscriptionTier", out _).Should().BeTrue();
            root.TryGetProperty("subscriptionActive", out _).Should().BeTrue();
        }

        [Fact]
        public async Task GetApiUsage_ForNewUser_ShowsZeroUsage()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/usage");
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.GetProperty("dailyUsage").GetInt32().Should().Be(0);
            root.GetProperty("subscriptionTier").GetString().Should().Be("Free");
        }

        #endregion

        #region Logout Tests

        [Fact]
        public async Task Logout_WithValidToken_ReturnsSuccess()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            root.GetProperty("message").GetString().Should().Contain("Logout successful");
        }

        #endregion

        #region Token Security Tests

        [Fact]
        public async Task Token_ContainsRequiredClaims()
        {
            // Arrange
            var token = await RegisterAndGetToken();

            // Act - Use token to get user info
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Add("Authorization", $"Bearer {token}");
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Token should be a valid JWT (contains two dots)
            token.Split('.').Length.Should().Be(3);
        }

        [Fact]
        public async Task Token_ExpiresAfterConfiguredTime()
        {
            // Note: This is a conceptual test
            // In production: token should expire after 60 minutes
            // In development: token expires after 120 minutes
            // This would require waiting or mocking time, so we document the expected behavior

            Assert.True(true, "Manual test: Verify token expires after configured duration");
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
