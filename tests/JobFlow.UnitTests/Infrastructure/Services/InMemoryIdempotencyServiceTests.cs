namespace JobFlow.UnitTests.Infrastructure.Services;

using JobFlow.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using System.Text;

public class InMemoryIdempotencyServiceTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly InMemoryIdempotencyService _service;

    public InMemoryIdempotencyServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _service = new InMemoryIdempotencyService(_cacheMock.Object);
    }

    [Fact]
    public async Task TryAcquireKeyAsync_ShouldReturnTrue_WhenKeyIsNotPresent()
    {
        // Arrange
        var key = "test-key";
        _cacheMock.Setup(c => c.GetAsync(key, default)).ReturnsAsync((byte[]?)null);

        // Act
        var result = await _service.TryAcquireKeyAsync(key, TimeSpan.FromMinutes(1));

        // Assert
        Assert.True(result);
        _cacheMock.Verify(c => c.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
    }

    [Fact]
    public async Task TryAcquireKeyAsync_ShouldReturnFalse_WhenKeyIsPresent()
    {
        // Arrange
        var key = "test-key";
        _cacheMock.Setup(c => c.GetAsync(key, default)).ReturnsAsync(Encoding.UTF8.GetBytes("processing"));

        // Act
        var result = await _service.TryAcquireKeyAsync(key, TimeSpan.FromMinutes(1));

        // Assert
        Assert.False(result);
        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Never);
    }
}
