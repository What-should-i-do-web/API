using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using WhatShouldIDo.Application.Configuration;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Services;
using Xunit;

namespace WhatShouldIDo.Tests.Unit
{
    public class QuotaServiceTests
    {
        private readonly Mock<IQuotaStore> _mockStore;
        private readonly Mock<IEntitlementService> _mockEntitlement;
        private readonly Mock<ILogger<QuotaService>> _mockLogger;
        private readonly QuotaOptions _options;
        private readonly QuotaService _sut;

        public QuotaServiceTests()
        {
            _mockStore = new Mock<IQuotaStore>();
            _mockEntitlement = new Mock<IEntitlementService>();
            _mockLogger = new Mock<ILogger<QuotaService>>();
            _options = new QuotaOptions { DefaultFreeQuota = 5 };

            _sut = new QuotaService(
                _mockStore.Object,
                _mockEntitlement.Object,
                Options.Create(_options),
                _mockLogger.Object);
        }

        [Fact]
        public async Task GetRemainingAsync_WhenQuotaExists_ReturnsValue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            var result = await _sut.GetRemainingAsync(userId);

            // Assert
            result.Should().Be(3);
        }

        [Fact]
        public async Task GetRemainingAsync_WhenQuotaNotInitialized_ReturnsZero()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null);

            // Act
            var result = await _sut.GetRemainingAsync(userId);

            // Assert
            result.Should().Be(0);
        }

        [Fact]
        public async Task GetRemainingAsync_OnStoreError_ReturnsZeroAndLogs()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Store error"));

            // Act
            var result = await _sut.GetRemainingAsync(userId);

            // Assert
            result.Should().Be(0);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task TryConsumeAsync_WhenPremium_AlwaysSucceeds()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockEntitlement.Setup(e => e.IsPremiumAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.TryConsumeAsync(userId, 1);

            // Assert
            result.Should().BeTrue();
            _mockStore.Verify(s => s.CompareExchangeConsumeAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task TryConsumeAsync_WhenNonPremiumAndSufficientQuota_ConsumesAndSucceeds()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockEntitlement.Setup(e => e.IsPremiumAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null);
            _mockStore.Setup(s => s.SetAsync(userId, _options.DefaultFreeQuota, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockStore.Setup(s => s.CompareExchangeConsumeAsync(userId, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.TryConsumeAsync(userId, 1);

            // Assert
            result.Should().BeTrue();
            _mockStore.Verify(s => s.CompareExchangeConsumeAsync(userId, 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task TryConsumeAsync_WhenNonPremiumAndInsufficientQuota_Fails()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockEntitlement.Setup(e => e.IsPremiumAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);
            _mockStore.Setup(s => s.CompareExchangeConsumeAsync(userId, 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _sut.TryConsumeAsync(userId, 1);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task TryConsumeAsync_WithInvalidAmount_ThrowsArgumentException()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.TryConsumeAsync(userId, 0));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sut.TryConsumeAsync(userId, -1));
        }

        [Fact]
        public async Task TryConsumeAsync_OnStoreError_FailsClosedAndLogs()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockEntitlement.Setup(e => e.IsPremiumAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);
            _mockStore.Setup(s => s.CompareExchangeConsumeAsync(userId, 1, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Store error"));

            // Act
            var result = await _sut.TryConsumeAsync(userId, 1);

            // Assert
            result.Should().BeFalse(); // Fail closed
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task InitializeIfNeededAsync_WhenAlreadyInitialized_DoesNothing()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(5);

            // Act
            await _sut.InitializeIfNeededAsync(userId);

            // Assert
            _mockStore.Verify(s => s.SetAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InitializeIfNeededAsync_WhenPremium_DoesNotInitialize()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null);
            _mockEntitlement.Setup(e => e.IsPremiumAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _sut.InitializeIfNeededAsync(userId);

            // Assert
            _mockStore.Verify(s => s.SetAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InitializeIfNeededAsync_WhenNonPremiumAndNotInitialized_InitializesWithDefaultQuota()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockStore.Setup(s => s.GetAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null);
            _mockEntitlement.Setup(e => e.IsPremiumAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            await _sut.InitializeIfNeededAsync(userId);

            // Assert
            _mockStore.Verify(s => s.SetAsync(userId, 5, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
