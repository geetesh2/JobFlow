using JobFlow.Application.DTOs;
using JobFlow.Application.Interfaces;

namespace JobFlow.IntegrationTests;

public sealed class TestJobSearchService : IJobSearchService
{
    private readonly ITestJobStore _jobService;

    public TestJobSearchService(ITestJobStore jobService)
    {
        _jobService = jobService;
    }

    public Task<JobSearchResult> SearchJobsAsync(JobSearchRequest request, CancellationToken cancellationToken = default)
    {
        var jobs = _jobService.GetAll();
        Console.WriteLine($"[TestJobSearchService] Returning {jobs.Count} jobs for query '{request.Query}' status '{request.Status}'");
        var result = new JobSearchResult(jobs, request.Page, request.PageSize, jobs.Count);
        return Task.FromResult(result);
    }
}
