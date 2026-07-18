using JobFlow.Application.DTOs;
using JobFlow.Application.Interfaces;
using JobFlow.Infrastructure.Models;
using Nest;
using JobFlow.Domain.Enums;

namespace JobFlow.Infrastructure.Services;

public sealed class ElasticJobSearchService : IJobSearchService
{
    private readonly IElasticClient _elasticClient;

    public ElasticJobSearchService(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async Task<JobSearchResult> SearchJobsAsync(JobSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = new QueryContainer();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            query &= new QueryStringQuery
            {
                Query = request.Query,
                Fields = Infer.Fields<JobDocument>(f => f.Name, f => f.Payload),
                DefaultOperator = Operator.And
            };
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<JobStatus>(request.Status, true, out var status))
        {
            query &= new TermQuery
            {
                Field = Infer.Field<JobDocument>(f => f.Status),
                Value = status
            };
        }

        if (request.CreatedAfterUtc is not null || request.CreatedBeforeUtc is not null)
        {
            query &= new DateRangeQuery
            {
                Field = Infer.Field<JobDocument>(f => f.CreatedAtUtc),
                GreaterThanOrEqualTo = request.CreatedAfterUtc,
                LessThanOrEqualTo = request.CreatedBeforeUtc
            };
        }

        var sortDescriptor = new SortDescriptor<JobDocument>();
        var normalizedSortBy = request.SortBy?.ToLowerInvariant() ?? "createdatutc";

        sortDescriptor = normalizedSortBy switch
        {
            "updatedatutc" => request.SortOrder?.ToLowerInvariant() == "asc"
                ? sortDescriptor.Ascending(f => f.UpdatedAtUtc)
                : sortDescriptor.Descending(f => f.UpdatedAtUtc),
            _ => request.SortOrder?.ToLowerInvariant() == "asc"
                ? sortDescriptor.Ascending(f => f.CreatedAtUtc)
                : sortDescriptor.Descending(f => f.CreatedAtUtc),
        };

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var response = await _elasticClient.SearchAsync<JobDocument>(s => s
            .Query(q => query)
            .Sort(srt => sortDescriptor)
            .From((page - 1) * pageSize)
            .Size(pageSize), cancellationToken);

        var jobs = response.Documents.Select(MapToResponse);
        return new JobSearchResult(jobs, page, pageSize, response.Total);
    }

    private static JobResponse MapToResponse(JobDocument doc)
        => new JobResponse(doc.Id, doc.Name, doc.Status.ToString(), doc.CreatedAtUtc, doc.UpdatedAtUtc);
}
