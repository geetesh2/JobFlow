using System.Collections.Generic;
using JobFlow.Infrastructure.Models;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace JobFlow.Infrastructure.Services;

public sealed class MongoDbIndexInitializer
{
    private readonly IMongoCollection<JobDocument> _jobs;

    public MongoDbIndexInitializer(IMongoClient client, IConfiguration configuration)
    {
        var databaseName = configuration.GetValue("MongoDb:Database", "jobflow");
        var collectionName = configuration.GetValue("MongoDb:JobsCollection", "jobs");
        var database = client.GetDatabase(databaseName);
        _jobs = database.GetCollection<JobDocument>(collectionName);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var existingIndexesCursor = await _jobs.Indexes.ListAsync(cancellationToken);
        var existingIndexes = await existingIndexesCursor.ToListAsync(cancellationToken);
        var existingIndexNames = existingIndexes
            .Select(index => index.GetValue("name", "").AsString)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var models = new List<CreateIndexModel<JobDocument>>();

        if (!existingIndexNames.Contains("Status_1"))
        {
            models.Add(new CreateIndexModel<JobDocument>(
                Builders<JobDocument>.IndexKeys.Ascending(x => x.Status),
                new CreateIndexOptions { Name = "Status_1" }));
        }

        if (!existingIndexNames.Contains("CreatedAtUtc_1"))
        {
            models.Add(new CreateIndexModel<JobDocument>(
                Builders<JobDocument>.IndexKeys.Ascending(x => x.CreatedAtUtc),
                new CreateIndexOptions { Name = "CreatedAtUtc_1" }));
        }

        if (!existingIndexNames.Contains("UpdatedAtUtc_1"))
        {
            models.Add(new CreateIndexModel<JobDocument>(
                Builders<JobDocument>.IndexKeys.Ascending(x => x.UpdatedAtUtc),
                new CreateIndexOptions { Name = "UpdatedAtUtc_1" }));
        }

        if (!existingIndexNames.Contains("Name_1"))
        {
            models.Add(new CreateIndexModel<JobDocument>(
                Builders<JobDocument>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Name = "Name_1" }));
        }

        if (!existingIndexNames.Contains("TextSearch"))
        {
            models.Add(new CreateIndexModel<JobDocument>(
                Builders<JobDocument>.IndexKeys.Text(x => x.Name).Text(x => x.Payload),
                new CreateIndexOptions { Name = "TextSearch" }));
        }

        if (models.Count > 0)
        {
            await _jobs.Indexes.CreateManyAsync(models, cancellationToken);
        }
    }
}
