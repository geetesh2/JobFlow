using JobFlow.Application.Abstractions.Services;
using JobFlow.Domain.Entities;
using JobFlow.Infrastructure.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace JobFlow.Infrastructure.Services;

public sealed class MongoJobSynchronizer : IJobSynchronizer
{
    private readonly IMongoCollection<JobDocument> _jobs;

    public MongoJobSynchronizer(IMongoClient client, IConfiguration configuration)
    {
        var databaseName = configuration.GetValue("MongoDb:Database", "jobflow");
        var collectionName = configuration.GetValue("MongoDb:JobsCollection", "jobs");
        var database = client.GetDatabase(databaseName);
        _jobs = database.GetCollection<JobDocument>(collectionName);
    }

    public async Task UpsertJobAsync(Job job, IDictionary<string, object?>? metadata = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<JobDocument>.Filter.Eq(x => x.Id, job.Id);
        var update = Builders<JobDocument>.Update
            .Set(x => x.Name, job.Name)
            .Set(x => x.Status, job.Status)
            .Set(x => x.Priority, job.Priority)
            .Set(x => x.CreatedAtUtc, job.CreatedAtUtc)
            .Set(x => x.UpdatedAtUtc, job.UpdatedAtUtc)
            .Set(x => x.Payload, job.Payload)
            .Set(x => x.RetryCount, job.RetryCount)
            .Set(x => x.MaxRetries, job.MaxRetries)
            .Set(x => x.ProgressPercentage, job.ProgressPercentage)
            .Set(x => x.ErrorMessage, job.ErrorMessage)
            .Set(x => x.CreatedBy, job.CreatedBy)
            .Set(x => x.CompletedAtUtc, job.CompletedAtUtc)
            .Set(x => x.Metadata, metadata);

        await _jobs.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public Task SynchronizeAsync(Job job, IDictionary<string, object?>? metadata = null, CancellationToken ct = default)
        => UpsertJobAsync(job, metadata, ct);
}
