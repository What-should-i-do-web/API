using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using WhatShouldIDo.Infrastructure.Quota;
using Xunit;

namespace WhatShouldIDo.Tests.Unit
{
    public class InMemoryQuotaStoreTests
    {
        private readonly Mock<ILogger<InMemoryQuotaStore>> _mockLogger;
        private readonly InMemoryQuotaStore _sut;

        public InMemoryQuotaStoreTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryQuotaStore>>();
            _sut = new InMemoryQuotaStore(_mockLogger.Object);
        }

        [Fact]
        public async Task GetAsync_WhenQuotaNotSet_ReturnsNull()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var result = await _sut.GetAsync(userId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_AndGet_ReturnsSetValue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await _sut.SetAsync(userId, 10);

            // Act
            var result = await _sut.GetAsync(userId);

            // Assert
            result.Should().Be(10);
        }

        [Fact]
        public async Task SetAsync_WithNegativeValue_ThrowsArgumentException()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.SetAsync(userId, -1));
        }

        [Fact]
        public async Task CompareExchangeConsumeAsync_WithSufficientQuota_SucceedsAndDecrements()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await _sut.SetAsync(userId, 5);

            // Act
            var success = await _sut.CompareExchangeConsumeAsync(userId, 2);
            var remaining = await _sut.GetAsync(userId);

            // Assert
            success.Should().BeTrue();
            remaining.Should().Be(3);
        }

        [Fact]
        public async Task CompareExchangeConsumeAsync_WithInsufficientQuota_FailsAndDoesNotDecrement()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await _sut.SetAsync(userId, 2);

            // Act
            var success = await _sut.CompareExchangeConsumeAsync(userId, 5);
            var remaining = await _sut.GetAsync(userId);

            // Assert
            success.Should().BeFalse();
            remaining.Should().Be(2); // Unchanged
        }

        [Fact]
        public async Task CompareExchangeConsumeAsync_WithZeroAmount_ThrowsArgumentException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await _sut.SetAsync(userId, 5);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.CompareExchangeConsumeAsync(userId, 0));
        }

        [Fact]
        public async Task CompareExchangeConsumeAsync_WithNegativeAmount_ThrowsArgumentException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            await _sut.SetAsync(userId, 5);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.CompareExchangeConsumeAsync(userId, -1));
        }

        [Fact]
        public async Task CompareExchangeConsumeAsync_WhenQuotaNotInitialized_Fails()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var success = await _sut.CompareExchangeConsumeAsync(userId, 1);

            // Assert
            success.Should().BeFalse();
        }

        [Fact]
        public async Task CompareExchangeConsumeAsync_Concurrent20Requests_Only5Succeed()
        {
            // Arrange - simulates 20 concurrent requests from different threads
            var userId = Guid.NewGuid();
            await _sut.SetAsync(userId, 5);
            const int concurrentRequests = 20;

            // Act - launch 20 concurrent consumption attempts
            var tasks = Enumerable.Range(0, concurrentRequests)
                .Select(_ => Task.Run(async () => await _sut.CompareExchangeConsumeAsync(userId, 1)))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(r => r);
            var remaining = await _sut.GetAsync(userId);

            successCount.Should().Be(5, "only 5 credits were available");
            remaining.Should().Be(0, "all credits should be consumed");
        }

        [Fact]
        public async Task CompareExchangeConsumeAsync_ConcurrentExactQuota_ExactlyMaxSucceed()
        {
            // Arrange - test edge case where quota equals concurrent requests
            var userId = Guid.NewGuid();
            const int quota = 10;
            await _sut.SetAsync(userId, quota);

            // Act
            var tasks = Enumerable.Range(0, quota)
                .Select(_ => Task.Run(async () => await _sut.CompareExchangeConsumeAsync(userId, 1)))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(r => r);
            var remaining = await _sut.GetAsync(userId);

            successCount.Should().Be(quota);
            remaining.Should().Be(0);
        }

        [Fact]
        public async Task MultipleUsers_IndependentQuotas()
        {
            // Arrange
            var user1 = Guid.NewGuid();
            var user2 = Guid.NewGuid();
            await _sut.SetAsync(user1, 3);
            await _sut.SetAsync(user2, 7);

            // Act
            await _sut.CompareExchangeConsumeAsync(user1, 2);
            await _sut.CompareExchangeConsumeAsync(user2, 5);

            var user1Remaining = await _sut.GetAsync(user1);
            var user2Remaining = await _sut.GetAsync(user2);

            // Assert
            user1Remaining.Should().Be(1);
            user2Remaining.Should().Be(2);
        }
    }
}
