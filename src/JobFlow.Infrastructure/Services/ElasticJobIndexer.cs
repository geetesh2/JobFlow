using JobFlow.Infrastructure.Models;
using Nest;

namespace JobFlow.Infrastructure.Services;

public sealed class ElasticJobIndexer
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

    public async Task UpdateJobStatusAsync(Guid jobId, string status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        await _client.UpdateAsync<JobDocument, object>(jobId, u => u
            .Doc(new { Status = status, UpdatedAtUtc = updatedAtUtc }), cancellationToken);
    }
}
