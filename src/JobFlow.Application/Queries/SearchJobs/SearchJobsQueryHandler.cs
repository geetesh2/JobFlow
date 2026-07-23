using JobFlow.Application.DTOs;
using JobFlow.Application.Interfaces;
using MediatR;

namespace JobFlow.Application.Queries.SearchJobs;

public sealed class SearchJobsQueryHandler : IRequestHandler<SearchJobsQuery, JobSearchResult>
{
    private readonly IJobSearchService _jobSearchService;

    public SearchJobsQueryHandler(IJobSearchService jobSearchService)
    {
        _jobSearchService = jobSearchService;
    }

    public async Task<JobSearchResult> Handle(SearchJobsQuery request, CancellationToken cancellationToken)
    {
        var searchRequest = new JobSearchRequest(
            request.Query,
            request.Status,
            request.CreatedAfterUtc,
            request.CreatedBeforeUtc,
            request.Page,
            request.PageSize,
            request.SortBy,
            request.SortOrder);

        return await _jobSearchService.SearchJobsAsync(searchRequest, cancellationToken);
    }
}
