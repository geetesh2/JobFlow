using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.DTOs;
using JobFlow.Application.Interfaces;
using JobFlow.Contracts.Messages;
using JobFlow.Domain.Entities;
using JobFlow.Domain.Enums;
using Microsoft.Extensions.Logging;
using Polly;
using JobFlow.Infrastructure.Models;

namespace JobFlow.Infrastructure.Services;

public sealed class JobService : IJobService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly MongoJobSynchronizer _mongoJobSynchronizer;
    private readonly ElasticJobIndexer _elasticJobIndexer;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<JobService> _logger;

    public JobService(
        IApplicationDbContext dbContext,
        MongoJobSynchronizer mongoJobSynchronizer,
        ElasticJobIndexer elasticJobIndexer,
        ResiliencePipeline resiliencePipeline,
        ILogger<JobService> logger)
    {
        _dbContext = dbContext;
        _mongoJobSynchronizer = mongoJobSynchronizer;
        _elasticJobIndexer = elasticJobIndexer;
        _resiliencePipeline = resiliencePipeline;
        _logger = logger;
    }

    public async Task<JobResponse> CreateJobAsync(JobCreateRequest request, CancellationToken cancellationToken = default)
    {
        var priority = Enum.TryParse<JobPriority>(request.Priority, true, out var p) ? p : JobPriority.Normal;
        var payload = request.Payload.HasValue ? request.Payload.Value.GetRawText() : null;

        var job = new Job(request.Name, priority, payload, request.MaxRetries);
        await _dbContext.Jobs.AddAsync(job, cancellationToken);

        var message = new JobCreatedMessage(
            job.Id,
            job.Name,
            job.Priority.ToString(),
            payload,
            job.MaxRetries,
            job.CreatedAtUtc,
            Guid.NewGuid().ToString("N"),
            System.Diagnostics.Activity.Current?.Id);

        var outboxMessage = new OutboxMessage(
            nameof(JobCreatedMessage),
            JsonSerializer.Serialize(message));
        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var metadata = request.Metadata?.ToDictionary(x => x.Key, x => (object?)x.Value.GetRawText()) ?? new Dictionary<string, object?>();

        try
        {
            await _resiliencePipeline.ExecuteAsync(async ct => await _mongoJobSynchronizer.UpsertJobAsync(job, metadata, ct), cancellationToken);
            await _resiliencePipeline.ExecuteAsync(async ct => await _elasticJobIndexer.IndexJobAsync(new JobDocument
            {
                Id = job.Id,
                Name = job.Name,
                Status = job.Status,
                Priority = job.Priority,
                CreatedAtUtc = job.CreatedAtUtc,
                UpdatedAtUtc = job.UpdatedAtUtc,
                Payload = payload,
                Metadata = metadata
            }, ct), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to synchronize job {JobId} to MongoDB/Elasticsearch.", job.Id);
        }

        return MapToResponse(job);
    }

    public async Task<JobResponse?> GetJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync(new object[] { id }, cancellationToken);
        return job is null ? null : MapToResponse(job);
    }

    public async Task<IEnumerable<JobResponse>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.Jobs
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return jobs.Select(MapToResponse);
    }

    private static JobResponse MapToResponse(Job job) => new(
        job.Id, job.Name, job.Status.ToString(), job.Priority.ToString(),
        job.RetryCount, job.MaxRetries, job.ProgressPercentage,
        job.ErrorMessage, job.CreatedBy, job.CreatedAtUtc, job.UpdatedAtUtc, job.CompletedAtUtc);
}
