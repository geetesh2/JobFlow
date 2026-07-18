using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Application.DTOs;
using JobFlow.Application.Interfaces;
using JobFlow.Contracts.Messages;
using JobFlow.Domain.Entities;

namespace JobFlow.Infrastructure.Services;

public sealed class JobService : IJobService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IJobPublisher _jobPublisher;

    public JobService(
        IApplicationDbContext dbContext,
        IJobPublisher jobPublisher)
    {
        _dbContext = dbContext;
        _jobPublisher = jobPublisher;
    }

    public async Task<JobResponse> CreateJobAsync(JobCreateRequest request, CancellationToken cancellationToken = default)
    {
        var job = new Job(request.Name);
        await _dbContext.Jobs.AddAsync(job, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
}
