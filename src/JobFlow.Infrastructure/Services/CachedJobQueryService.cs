using System.Text.Json;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.DTOs;
using JobFlow.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace JobFlow.Infrastructure.Services;

public sealed class CachedJobQueryService
{
    private readonly IDistributedCache _cache;
    private readonly IJobRepository _repository;
    private readonly ILogger<CachedJobQueryService> _logger;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public CachedJobQueryService(
        IDistributedCache cache,
        IJobRepository repository,
        ILogger<CachedJobQueryService> logger)
    {
        _cache = cache;
        _repository = repository;
        _logger = logger;
    }

    public async Task<JobResponse?> GetJobByIdCachedAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"job:{id}";
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for job {JobId}", id);
            return JsonSerializer.Deserialize<JobResponse>(cached);
        }

        var job = await _repository.GetByIdAsync(id, ct);
        if (job is null) return null;

        var response = MapToResponse(job);
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiration },
            ct);

        _logger.LogDebug("Cache miss for job {JobId}, cached result", id);
        return response;
    }

    public async Task InvalidateCacheAsync(Guid jobId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync($"job:{jobId}", ct);
        _logger.LogDebug("Invalidated cache for job {JobId}", jobId);
    }

    private static JobResponse MapToResponse(Job job) => new(
        job.Id, job.Name, job.Status.ToString(), job.Priority.ToString(),
        job.RetryCount, job.MaxRetries, job.ProgressPercentage,
        job.ErrorMessage, job.CreatedBy, job.CreatedAtUtc, job.UpdatedAtUtc, job.CompletedAtUtc);
}
