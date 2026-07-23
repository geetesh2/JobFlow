using FluentAssertions;
using JobFlow.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using System.Text;

namespace JobFlow.UnitTests.Infrastructure.Services;

public class RedisIdempotencyServiceTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly RedisIdempotencyService _service;

    public RedisIdempotencyServiceTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _service = new RedisIdempotencyService(_cacheMock.Object);
    }

    [Fact]
    public async Task TryAcquireKeyAsync_ShouldReturnTrue_WhenKeyIsNotPresent()
    {
        var key = "test-key";
        _cacheMock.Setup(c => c.GetAsync(key, default)).ReturnsAsync((byte[]?)null);

        var result = await _service.TryAcquireKeyAsync(key, TimeSpan.FromMinutes(1));

        result.Should().BeTrue();
        _cacheMock.Verify(c => c.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
    }

    [Fact]
    public async Task TryAcquireKeyAsync_ShouldReturnFalse_WhenKeyIsPresent()
    {
        var key = "test-key";
        _cacheMock.Setup(c => c.GetAsync(key, default)).ReturnsAsync(Encoding.UTF8.GetBytes("processing"));

        var result = await _service.TryAcquireKeyAsync(key, TimeSpan.FromMinutes(1));

        result.Should().BeFalse();
        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Never);
    }
}
