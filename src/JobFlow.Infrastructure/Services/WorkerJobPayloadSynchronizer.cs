using JobFlow.Domain.Enums;
using JobFlow.Infrastructure.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace JobFlow.Infrastructure.Services;

public sealed class WorkerJobStatusUpdater
{
    private readonly IMongoCollection<JobDocument> _jobs;
    private readonly ElasticJobIndexer _elasticJobIndexer;

    public WorkerJobStatusUpdater(
        IMongoClient mongoClient,
        IConfiguration configuration,
        ElasticJobIndexer elasticJobIndexer)
    {
        var databaseName = configuration.GetValue("MongoDb:Database", "jobflow");
        var collectionName = configuration.GetValue("MongoDb:JobsCollection", "jobs");
        _jobs = mongoClient.GetDatabase(databaseName).GetCollection<JobDocument>(collectionName);
        _elasticJobIndexer = elasticJobIndexer;
    }

    public async Task<JobDocument?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _jobs.Find(x => x.Id == jobId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(Guid jobId, JobStatus status, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<JobDocument>.Filter.Eq(x => x.Id, jobId);
        var update = Builders<JobDocument>.Update
            .Set(x => x.Status, status)
            .Set(x => x.UpdatedAtUtc, updatedAtUtc);

        await _jobs.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        await _elasticJobIndexer.UpdateJobStatusAsync(jobId, status, updatedAtUtc, cancellationToken);
    }
}
