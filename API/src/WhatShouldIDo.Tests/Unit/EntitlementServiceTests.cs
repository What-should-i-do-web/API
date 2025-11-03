using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using WhatShouldIDo.Infrastructure.Services;
using Xunit;

namespace WhatShouldIDo.Tests.Unit
{
    public class EntitlementServiceTests
    {
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly Mock<ILogger<EntitlementService>> _mockLogger;
        private readonly EntitlementService _sut;

        public EntitlementServiceTests()
        {
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockLogger = new Mock<ILogger<EntitlementService>>();
            _sut = new EntitlementService(_mockHttpContextAccessor.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task IsPremiumAsync_WhenNotAuthenticated_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var context = new DefaultHttpContext();
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPremiumAsync_WhenNoHttpContext_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPremiumAsync_WithPremiumSubscriptionClaim_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("subscription", "premium")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsPremiumAsync_WithFreeSubscriptionClaim_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("subscription", "free")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPremiumAsync_WithPremiumRoleClaim_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, "premium")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsPremiumAsync_WithNonPremiumRoleClaim_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, "user")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPremiumAsync_WithNoSubscriptionOrRoleClaims_ReturnsFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("email", "user@test.com")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task IsPremiumAsync_CaseInsensitiveSubscriptionCheck_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("subscription", "PREMIUM") // uppercase
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsPremiumAsync_WithMultipleRoles_OnePremium_ReturnsTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var claims = new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Role, "user"),
                new Claim(ClaimTypes.Role, "premium"),
                new Claim(ClaimTypes.Role, "admin")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            var context = new DefaultHttpContext { User = principal };
            _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(context);

            // Act
            var result = await _sut.IsPremiumAsync(userId);

            // Assert
            result.Should().BeTrue();
        }
    }
}
