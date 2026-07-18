using JobFlow.Application.Abstractions.Persistence;
using JobFlow.Domain.Entities;
using JobFlow.Infrastructure.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace JobFlow.Infrastructure.Services;

public sealed class MongoJobSynchronizer
{
    private readonly IMongoCollection<JobDocument> _jobs;

    public MongoJobSynchronizer(IMongoClient client, IConfiguration configuration)
    {
        var databaseName = configuration.GetValue("MongoDb:Database", "jobflow");
        var collectionName = configuration.GetValue("MongoDb:JobsCollection", "jobs");
        var database = client.GetDatabase(databaseName);
        _jobs = database.GetCollection<JobDocument>(collectionName);
    }

    public async Task UpsertJobAsync(Job job, string? payload, IDictionary<string, object?> metadata, CancellationToken cancellationToken = default)
    {
        var filter = Builders<JobDocument>.Filter.Eq(x => x.Id, job.Id);
        var update = Builders<JobDocument>.Update
            .Set(x => x.Name, job.Name)
            .Set(x => x.Status, job.Status)
            .Set(x => x.CreatedAtUtc, job.CreatedAtUtc)
            .Set(x => x.UpdatedAtUtc, job.UpdatedAtUtc)
            .Set(x => x.Payload, payload ?? string.Empty)
            .Set(x => x.Metadata, metadata);

        await _jobs.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }
}
