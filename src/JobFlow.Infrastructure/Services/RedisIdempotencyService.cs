using JobFlow.Application.Abstractions.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace JobFlow.Infrastructure.Services;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IDistributedCache _cache;

    public RedisIdempotencyService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> IsRequestProcessingAsync(string key)
    {
        return await _cache.GetStringAsync(key) != null;
    }

    public async Task<bool> TryAcquireKeyAsync(string key, TimeSpan expiration)
    {
        if (await IsRequestProcessingAsync(key))
        {
            return false;
        }

        await _cache.SetStringAsync(key, "processing", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        });

        return true;
    }

    public async Task ReleaseKeyAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }
}
