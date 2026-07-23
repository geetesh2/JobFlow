namespace JobFlow.Application.Abstractions.Services;

public interface IIdempotencyService
{
    Task<bool> IsRequestProcessingAsync(string key);
    Task<bool> TryAcquireKeyAsync(string key, TimeSpan expiration);
    Task ReleaseKeyAsync(string key);
}
