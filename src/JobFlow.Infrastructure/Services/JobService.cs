using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.DTOs;
using JobFlow.Application.Interfaces;
using JobFlow.Contracts.Messages;
using JobFlow.Domain.Entities;
using Microsoft.Extensions.Logging;
using Polly;
using JobFlow.Infrastructure.Models;

namespace JobFlow.Infrastructure.Services;

public sealed class JobService : IJobService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IJobPublisher _jobPublisher;
    private readonly MongoJobSynchronizer _mongoJobSynchronizer;
    private readonly ElasticJobIndexer _elasticJobIndexer;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<JobService> _logger;

    public JobService(
        IApplicationDbContext dbContext,
        IJobPublisher jobPublisher,
        MongoJobSynchronizer mongoJobSynchronizer,
        ElasticJobIndexer elasticJobIndexer,
        ResiliencePipeline resiliencePipeline,
        ILogger<JobService> logger)
    {
        _dbContext = dbContext;
        _jobPublisher = jobPublisher;
        _mongoJobSynchronizer = mongoJobSynchronizer;
        _elasticJobIndexer = elasticJobIndexer;
        _resiliencePipeline = resiliencePipeline;
        _logger = logger;
    }

    public async Task<JobResponse> CreateJobAsync(JobCreateRequest request, CancellationToken cancellationToken = default)
    {
        var job = new Job(request.Name);
        await _dbContext.Jobs.AddAsync(job, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var payload = request.Payload.HasValue ? request.Payload.Value.GetRawText() : string.Empty;
        var metadata = request.Metadata?.ToDictionary(x => x.Key, x => (object?)x.Value.GetRawText()) ?? new Dictionary<string, object?>();

        try
        {
            await _resiliencePipeline.ExecuteAsync(async ct => await _mongoJobSynchronizer.UpsertJobAsync(job, payload, metadata, ct), cancellationToken);
            await _resiliencePipeline.ExecuteAsync(async ct => await _elasticJobIndexer.IndexJobAsync(new JobDocument
            {
                Id = job.Id,
                Name = job.Name,
                Status = job.Status,
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

        var message = new JobCreatedMessage(
            job.Id,
            job.Name,
            job.CreatedAtUtc,
            Guid.NewGuid().ToString("N"));

        await _jobPublisher.PublishJobCreatedAsync(message, cancellationToken);

        return new JobResponse(job.Id, job.Name, job.Status.ToString(), job.CreatedAtUtc, job.UpdatedAtUtc);
    }

    public async Task<JobResponse?> GetJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync(new object[] { id }, cancellationToken);
        return job is null ? null : new JobResponse(job.Id, job.Name, job.Status.ToString(), job.CreatedAtUtc, job.UpdatedAtUtc);
    }

    public async Task<IEnumerable<JobResponse>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.Jobs
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return jobs.Select(j => new JobResponse(j.Id, j.Name, j.Status.ToString(), j.CreatedAtUtc, j.UpdatedAtUtc));
    }
}
