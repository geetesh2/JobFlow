using System.Collections.Generic;
using JobFlow.Infrastructure.Models;
using Nest;

namespace JobFlow.Infrastructure.Services;

public sealed class ElasticsearchIndexInitializer
{
    private readonly IElasticClient _elasticClient;

    public ElasticsearchIndexInitializer(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var indexName = _elasticClient.ConnectionSettings.DefaultIndex;

        var exists = await _elasticClient.Indices.ExistsAsync(indexName, ct: cancellationToken);
        if (exists.Exists)
        {
            return;
        }

        var response = await _elasticClient.Indices.CreateAsync(indexName, c => c
            .Settings(s => s
                .NumberOfShards(1)
                .NumberOfReplicas(0)
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("standard_lowercase", ca => ca
                            .Tokenizer("standard")
                            .Filters("lowercase", "asciifolding")))))
            .Map<JobDocument>(m => m
                .AutoMap()
                .Properties(ps => ps
                    .Text(t => t
                        .Name(n => n.Name)
                        .Analyzer("standard_lowercase"))
                    .Text(t => t
                        .Name(n => n.Payload)
                        .Analyzer("standard_lowercase"))
                    .Keyword(k => k
                        .Name(n => n.Status))
                    .Date(d => d
                        .Name(n => n.CreatedAtUtc))
                    .Date(d => d
                        .Name(n => n.UpdatedAtUtc))
                    .Object<IDictionary<string, object?>>(o => o
                        .Name(n => n.Metadata)
                        .Enabled(false)))));

        if (!response.IsValid)
        {
            throw new InvalidOperationException("Failed to create Elasticsearch index: " + response.DebugInformation);
        }
    }
}
