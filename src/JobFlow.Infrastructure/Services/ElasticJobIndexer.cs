using JobFlow.Application.Abstractions.Services;
using JobFlow.Domain.Enums;
using JobFlow.Infrastructure.Models;
using Nest;
using DomainJob = JobFlow.Domain.Entities.Job;

namespace JobFlow.Infrastructure.Services;

public sealed class ElasticJobIndexer : IJobIndexer
{
    private readonly IElasticClient _client;

    public ElasticJobIndexer(IElasticClient client)
    {
        _client = client;
    }

    public async Task IndexJobAsync(JobDocument job, CancellationToken cancellationToken = default)
    {
        await _client.IndexDocumentAsync(job, cancellationToken);
    }

    public async Task IndexAsync(DomainJob job, string? payload, IDictionary<string, object?>? metadata = null, CancellationToken ct = default)
    {
        var document = new JobDocument
        {
            Id = job.Id,
            Name = job.Name,
            Status = job.Status,
            Priority = job.Priority,
            CreatedAtUtc = job.CreatedAtUtc,
            UpdatedAtUtc = job.UpdatedAtUtc,
            Payload = payload,
            Metadata = metadata
        };

        await IndexJobAsync(document, ct);
    }

    public async Task UpdateJobStatusAsync(Guid jobId, JobStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        await _client.UpdateAsync<JobDocument, object>(jobId, u => u
            .Doc(new { Status = status, UpdatedAtUtc = updatedAtUtc }), cancellationToken);
    }
}
